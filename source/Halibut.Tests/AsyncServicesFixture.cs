using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class AsyncServicesFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testAsyncServicesAsWell: true)]
        public async Task AsyncServicesCanBeRegisteredAndResolved(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var clientAndServiceBuilder = clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder();

            if (clientAndServiceTestCase.ServiceAsyncHalibutFeature.IsEnabled())
            {
                clientAndServiceBuilder = clientAndServiceBuilder
                    .WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
            }
            else
            {
                clientAndServiceBuilder = clientAndServiceBuilder
                    .WithService<IEchoService>(() => new EchoService());
            }

            var clientAndService = await clientAndServiceBuilder.Build(CancellationToken);

            var value = Some.RandomAsciiStringOfLength(8);
            string result = null;
            await clientAndServiceTestCase.ForceClientProxyType.ToSyncOrAsync()
                .WhenSync(() =>
                {
                    var echoServiceClient = clientAndService.Client.CreateClient<IEchoService>(clientAndService.ServiceEndPoint, CancellationToken);
                    result = echoServiceClient.SayHello(value);
                })
                .WhenAsync(async () =>
                {
                    var echoServiceClient = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                    result = await echoServiceClient.SayHelloAsync(value);
                });

            if (clientAndServiceTestCase.ServiceAsyncHalibutFeature.IsEnabled())
            {
                result.Should().Be($"{value}Async...");
            }
            else
            {
                result.Should().Be($"{value}...");
            }
        }
    }
}
