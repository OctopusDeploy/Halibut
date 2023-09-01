using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Observability
{
    public class RpcObservabilityFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions:false, testSyncClients:false)]
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

                await echo.SayHelloAsync("Hello");
                await AssertionExtensions.Should(() => echo.CrashAsync()).ThrowAsync<ServiceInvocationHalibutClientException>();

                rpcObserver.StartCalls.Should().BeEquivalentTo(nameof(echo.SayHelloAsync), nameof(echo.CrashAsync));
                rpcObserver.EndCalls.Should().BeEquivalentTo(nameof(echo.SayHelloAsync), nameof(echo.CrashAsync));
            }
        }
    }
}