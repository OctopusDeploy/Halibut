using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Observability
{
    public class RpcObservabilityFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions:false, testSyncClients:false)]
        public async Task RpcCallsShouldBeObserved_RecordsStartAndEndWhenValueIsReturned(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .WithStandardServices()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();

                await echo.SayHelloAsync("Hello");
                
                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testSyncClients: false)]
        public async Task RpcCallsShouldBeObserved_RecordsStartAndEndWhenMethodIsVoid(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .WithStandardServices()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                
                await echo.ReturnNothingAsync();

                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.ReturnNothing));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.ReturnNothing));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testSyncClients: false)]
        public async Task RpcCallsShouldBeObserved_RegardlessOfSuccessOrFailure(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .WithStandardServices()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                
                await AssertionExtensions.Should(() => echo.CrashAsync()).ThrowAsync<ServiceInvocationHalibutClientException>();

                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.Crash));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.Crash));
            }
        }

        static void ThenShouldContainOneCall(IReadOnlyList<RequestMessage> calls, string expectedService, string expectedMethodCall)
        {
            var call = calls.Should().ContainSingle().Subject;

            call.ServiceName.Should().Be(expectedService);
            call.MethodName.Should().Be(expectedMethodCall);
        }
    }
}