using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
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

                var echo = clientAndService.CreateClient<ICountingService, IClientCountingService>(point =>
                    {
                        point.RetryCountLimit = 1000000;
                        point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    });

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                Assert.That(() => echo.Increment(new HalibutProxyRequestOptions(cts.Token)), Throws.Exception
                    .With.Message.Contains("The operation was canceled"));
                
                clientAndService.PortForwarder.ReturnToNormalMode();
                
                echo.Increment(new HalibutProxyRequestOptions(CancellationToken));

                echo.GetCurrentValue(new HalibutProxyRequestOptions(CancellationToken))
                    .Should().Be(1, "Since we cancelled the first call");
            }
        }

        [Test]
        public async Task CannotHaveServiceWithHalibutProxyRequestOptions()
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .NoService()
                       .WithService<IAmNotAllowed>(() => new AmNotAllowed())
                       .Build(CancellationToken))
            {
                Assert.Throws<TypeNotAllowedException>(() => clientAndService.CreateClient<IAmNotAllowed>());
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task HalibutProxyRequestOptionsCanBeSentToLatestAndOldServicesThatPreDateHalibutProxyRequestOptions(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IClientEchoService>();

                echo.SayHello("Hello!!", new HalibutProxyRequestOptions(new CancellationToken()))
                    .Should()
                    .Be("Hello!!...");
            }
        }
        
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task HalibutProxyRequestOptions_CanNotCancel_InFlightRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var lockService = clientAndService.CreateClient<ILockService, IClientLockService>();

                var cts = new CancellationTokenSource();
                using var tmpDir = new TemporaryDirectory();
                var fileThatOnceDeletedEndsTheCall = tmpDir.CreateRandomFile();
                var callStartedFile = tmpDir.RandomFileName();

                var inFlightRequest = Task.Run(() => lockService.WaitForFileToBeDeleted(fileThatOnceDeletedEndsTheCall, callStartedFile, new HalibutProxyRequestOptions(CancellationToken.None)));

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
                
                inFlightRequest.Status.Should().Be(TaskStatus.Running, "The cancellation token can not cancel in flight requests.");
                
                File.Delete(fileThatOnceDeletedEndsTheCall);

                // Now the lock is released we should be able to complete the request.
                await inFlightRequest;
            }
        }

        public interface IClientEchoService
        {
            int LongRunningOperation(HalibutProxyRequestOptions halibutProxyRequestOptions);

            string SayHello(string name, HalibutProxyRequestOptions halibutProxyRequestOptions);

            bool Crash(HalibutProxyRequestOptions halibutProxyRequestOptions);

            int CountBytes(DataStream stream, HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
        
        public interface IClientLockService
        {
            public void WaitForFileToBeDeleted(string fileToWaitFor, string fileSignalWhenRequestIsStarted, HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
        
        public interface IClientCountingService
        {
            public int Increment(HalibutProxyRequestOptions halibutProxyRequestOptions);
            public int GetCurrentValue(HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
    }

    public interface IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts);
    }

    public class AmNotAllowed : IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts)
        {
            throw new NotImplementedException();
        }
    }
}
