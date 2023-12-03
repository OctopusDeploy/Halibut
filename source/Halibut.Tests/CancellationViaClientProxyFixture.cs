using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.ExtensionMethods;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class CancellationViaClientProxyFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task HalibutProxyRequestOptions_ConnectingCancellationToken_CanCancel_ConnectingOrQueuedRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var tokenSourceToCancel = new CancellationTokenSource();
            var halibutRequestOption = new HalibutProxyRequestOptions(tokenSourceToCancel.Token, CancellationToken.None);

            await CanCancel_ConnectingOrQueuedRequests(clientAndServiceTestCase, tokenSourceToCancel, halibutRequestOption);
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task HalibutProxyRequestOptions_InProgressCancellationToken_CanCancel_ConnectingOrQueuedRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var tokenSourceToCancel = new CancellationTokenSource();
            var halibutRequestOption = new HalibutProxyRequestOptions(CancellationToken.None, tokenSourceToCancel.Token);

            await CanCancel_ConnectingOrQueuedRequests(clientAndServiceTestCase, tokenSourceToCancel, halibutRequestOption);
        }

        async Task CanCancel_ConnectingOrQueuedRequests(ClientAndServiceTestCase clientAndServiceTestCase, CancellationTokenSource tokenSourceToCancel, HalibutProxyRequestOptions halibutRequestOption)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithPortForwarding(out var portForwarderRef, port => PortForwarderUtil.ForwardingToLocalPort(port).Build())
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                portForwarderRef.Value.EnterKillNewAndExistingConnectionsMode();
                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateAsyncClient<ICountingService, IAsyncClientCountingServiceWithOptions>(
                    point => point.TryAndConnectForALongTime());
                
                tokenSourceToCancel.CancelAfter(TimeSpan.FromMilliseconds(100));
                
                (await AssertException.Throws<Exception>(() => echo.IncrementAsync(halibutRequestOption)))
                    .And.Message.Contains("The operation was canceled");

                portForwarderRef.Value.ReturnToNormalMode();
                
                await echo.IncrementAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None));

                (await echo.GetCurrentValueAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None)))
                    .Should().Be(1, "Since we cancelled the first call");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task HalibutProxyRequestOptions_ConnectingCancellationToken_CanNotCancel_InFlightRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithHalibutLoggingLevel(LogLevel.Trace)
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var lockService = clientAndService.CreateAsyncClient<ILockService, IAsyncClientLockServiceWithOptions>();

                var tokenSourceToCancel = new CancellationTokenSource();
                using var tmpDir = new TemporaryDirectory();
                var fileThatOnceDeletedEndsTheCall = tmpDir.CreateRandomFile();
                var callStartedFile = tmpDir.RandomFileName();

                var inFlightRequest = Task.Run(async () => await lockService.WaitForFileToBeDeletedAsync(
                    fileThatOnceDeletedEndsTheCall,
                    callStartedFile,
                    new HalibutProxyRequestOptions(tokenSourceToCancel.Token, CancellationToken.None)));

                Logger.Information("Waiting for the RPC call to be inflight");
                while (!File.Exists(callStartedFile))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
                }

                // The call is now in flight. Call cancel on the cancellation token for that in flight request.
                tokenSourceToCancel.Cancel();

                // Give time for the cancellation to do something
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);

                if (inFlightRequest.Status == TaskStatus.Faulted) await inFlightRequest;

                inFlightRequest.IsCompleted.Should().Be(false, $"The cancellation token can not cancel in flight requests. Current state: {inFlightRequest.Status}");

                File.Delete(fileThatOnceDeletedEndsTheCall);

                // Now the lock is released we should be able to complete the request.
                await inFlightRequest;
            }
        }

        [Test]
// TODO: ASYNC ME UP!
// net48 does not support cancellation of the request as the DeflateStream ends up using Begin and End methods which don't get passed the cancellation token
#if NETFRAMEWORK
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening:false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false, testListening:false)]
#else
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
#endif
        public async Task HalibutProxyRequestOptions_InProgressCancellationToken_CanCancel_InFlightRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var lockService = clientAndService.CreateAsyncClient<ILockService, IAsyncClientLockServiceWithOptions>();

                var tokenSourceToCancel = new CancellationTokenSource();
                using var tmpDir = new TemporaryDirectory();
                var fileThatOnceDeletedEndsTheCall = tmpDir.CreateRandomFile();
                var callStartedFile = tmpDir.RandomFileName();

                var inFlightRequest = Task.Run(async () =>
                {
                    await lockService.WaitForFileToBeDeletedAsync(
                        fileThatOnceDeletedEndsTheCall,
                        callStartedFile,
                        new HalibutProxyRequestOptions(CancellationToken.None, tokenSourceToCancel.Token));
                });

                Logger.Information("Waiting for the RPC call to be inflight");
                while (!File.Exists(callStartedFile))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
                }
                
                // The call is now in flight. Call cancel on the cancellation token for that in flight request.
                tokenSourceToCancel.Cancel();
                
                // Give time for the cancellation to do something
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);

                (await AssertException.Throws<Exception>(inFlightRequest))
                    .And
                    .Should().Match<Exception>(x => x is OperationCanceledException || (x.GetType() == typeof(HalibutClientException) && x.Message.Contains("The ReadAsync operation was cancelled")));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testWebSocket: false, testPolling:false)]
        public async Task CannotHaveServiceWithHalibutProxyRequestOptions(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
            {
                Assert.Throws<TypeNotAllowedException>(() =>
                {
                    client.CreateClientWithoutService<IAmNotAllowed, IAsyncClientAmNotAllowed>();
                });
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task HalibutProxyRequestOptionsCanBeSentToLatestAndOldServicesThatPreDateHalibutProxyRequestOptions(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoServiceWithOptions>();

                (await echo.SayHelloAsync("Hello!!", new HalibutProxyRequestOptions(CancellationToken, null)))
                    .Should()
                    .Be("Hello!!...");
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
}
