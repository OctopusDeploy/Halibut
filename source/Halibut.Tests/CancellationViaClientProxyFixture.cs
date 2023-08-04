using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.TestServices.SyncClientWithOptions;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class CancellationViaClientProxyFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task CancellationCanBeDoneViaClientProxy(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port).Build())
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                
                clientAndService.PortForwarder.EnterKillNewAndExistingConnectionsMode();
                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClientWithOptions<ICountingService, ISyncClientCountingServiceWithOptions, IAsyncClientCountingServiceWithOptions>(point =>
                    {
                        point.RetryCountLimit = 1000000;
                        point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    });

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));
                
                (await AssertAsync.Throws<Exception>(() => echo.IncrementAsync(new HalibutProxyRequestOptions(cts.Token))))
                    .And
                    .Message.Contains("The operation was canceled");

                clientAndService.PortForwarder.ReturnToNormalMode();
                
                await echo.IncrementAsync(new HalibutProxyRequestOptions(CancellationToken));

                (await echo.GetCurrentValueAsync(new HalibutProxyRequestOptions(CancellationToken)))
                    .Should().Be(1, "Since we cancelled the first call");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false, testAsyncAndSyncClients: true)]
        public async Task CannotHaveServiceWithHalibutProxyRequestOptions(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .NoService()
                       .WithService<IAmNotAllowed>(() => new AmNotAllowed())
                       .Build(CancellationToken))
            {
                if (clientAndServiceTestCase.SyncOrAsync == SyncOrAsync.Async)
                {
                    Assert.Throws<TypeNotAllowedException>(() =>
                    {
                        clientAndService.Client.CreateAsyncClient<IAmNotAllowed, IAsyncClientAmNotAllowed>(clientAndService.ServiceEndpoint());
                    });
                }
                if (clientAndServiceTestCase.SyncOrAsync == SyncOrAsync.Sync)
                {
                    Assert.Throws<TypeNotAllowedException>(() => clientAndService.Client.CreateClient<IAmNotAllowed>(clientAndService.ServiceEndpoint()));
                }
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task HalibutProxyRequestOptionsCanBeSentToLatestAndOldServicesThatPreDateHalibutProxyRequestOptions(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClientWithOptions<IEchoService, ISyncClientEchoServiceWithOptions, IAsyncClientEchoServiceWithOptions>();

                (await echo.SayHelloAsync("Hello!!", new HalibutProxyRequestOptions(new CancellationToken())))
                    .Should()
                    .Be("Hello!!...");
            }
        }
        
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task HalibutProxyRequestOptions_CanNotCancel_InFlightRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var lockService = clientAndService.CreateClientWithOptions<ILockService, ISyncClientLockServiceWithOptions, IAsyncClientLockServiceWithOptions>();

                var cts = new CancellationTokenSource();
                using var tmpDir = new TemporaryDirectory();
                var fileThatOnceDeletedEndsTheCall = tmpDir.CreateRandomFile();
                var callStartedFile = tmpDir.RandomFileName();

                var inFlightRequest = Task.Run(async () => await lockService.WaitForFileToBeDeletedAsync(fileThatOnceDeletedEndsTheCall, callStartedFile, new HalibutProxyRequestOptions(CancellationToken.None)));

                Logger.Information("Waiting for the RPC call to be inflight");
                while (!File.Exists(callStartedFile))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
                }
                
                // The call is now in flight.
                // Call cancel on the cancellation token for that in flight request.
                cts.Cancel();
                
                // Give time for the cancellation to do something
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
                
                if (inFlightRequest.Status == TaskStatus.Faulted) await inFlightRequest;
                
                inFlightRequest.IsCompleted.Should().Be(false, $"The cancellation token can not cancel in flight requests. Current state: {inFlightRequest.Status}");
                
                File.Delete(fileThatOnceDeletedEndsTheCall);

                // Now the lock is released we should be able to complete the request.
                await inFlightRequest;
            }
        }
    }

    public interface IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts);
    }
    
    public interface IAsyncClientAmNotAllowed
    {
        public Task FooAsync(HalibutProxyRequestOptions opts);
    }

    public class AmNotAllowed : IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts)
        {
            throw new NotImplementedException();
        }
    }
}
