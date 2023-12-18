using System;
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
        [LatestClientAndLatestServiceTestCases(testNetworkConditions:false)]
        public async Task RpcCallsShouldBeObserved_RecordsStartAndEndWhenValueIsReturned(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .WithStandardServices()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                await echo.SayHelloAsync("Hello");
                
                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task RpcCallsShouldBeObserved_EvenWhenRpcCallFails(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .WithStandardServices()
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                
                await AssertionExtensions.Should(() => echo.CrashAsync()).ThrowAsync<ServiceInvocationHalibutClientException>();

                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.Crash));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.Crash));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task RpcCallsShouldBeObserved_EvenIfServiceDoesNotExist(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var rpcObserver = new TestRpcObserver();

            await using (var clientAndService = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder()
                             .AsLatestClientBuilder()
                             .WithClientRpcObserver(rpcObserver)
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClientWithoutService<IEchoService, IAsyncClientEchoService>(point => point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(1));

                await AssertionExtensions.Should(() => echo.SayHelloAsync("Hello")).ThrowAsync<HalibutClientException>();

                ThenShouldContainOneCall(rpcObserver.StartCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
                ThenShouldContainOneCall(rpcObserver.EndCalls, nameof(IEchoService), nameof(IEchoService.SayHello));
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