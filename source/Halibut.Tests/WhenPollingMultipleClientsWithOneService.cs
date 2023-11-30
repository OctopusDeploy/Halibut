using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenPollingMultipleClientsWithOneService : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions:false, testListening:false)]
        public async Task RequestsShouldBeTakenFromAnyClient(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new AsyncCountingService();
            await using (var clientOnly1 = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
            {
                await using (var clientOnly2 = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
                {
                    var clients = new[]
                    {
                        clientOnly1.ListeningUri!,
                        clientOnly2.ListeningUri!
                    };

                    await using (var service = await clientAndServiceTestCase.CreateServiceOnlyTestCaseBuilder()
                                     .AsLatestServiceBuilder()
                                     .WithListeningClients(clients)
                                     .WithCountingService(countingService)
                                     .Build(CancellationToken))
                    {

                        var clientCountingService1 = clientOnly1.CreateClient<ICountingService, IAsyncClientCountingService>(service.ServiceUri);
                        var clientCountingService2 = clientOnly2.CreateClient<ICountingService, IAsyncClientCountingService>(service.ServiceUri);

                        await clientCountingService1.IncrementAsync();

                        countingService.CurrentValue().Should().Be(1);

                        await clientCountingService2.IncrementAsync();

                        countingService.CurrentValue().Should().Be(2);
                    }
                }
            }
        }
    }
}
