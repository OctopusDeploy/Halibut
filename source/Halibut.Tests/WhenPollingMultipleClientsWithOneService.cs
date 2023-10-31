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
            var countingService = new CountingService();
            await using (var clientOnly1 = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .NoService()
                             .Build(CancellationToken))
            {
                await using (var clientOnly2 = await clientAndServiceTestCase.CreateTestCaseBuilder()
                                 .AsLatestClientAndLatestServiceBuilder()
                                 .NoService()
                                 .Build(CancellationToken))
                {
                    var clients = new[]
                    {
                        clientOnly1.ClientUri!,
                        clientOnly2.ClientUri!
                    };

                    await using (await clientAndServiceTestCase.CreateTestCaseBuilder()
                                     .AsLatestClientAndLatestServiceBuilder()
                                     .WithPollingClients(clients)
                                     .WithCountingService(countingService)
                                     .Build(CancellationToken))
                    {

                        var clientCountingService1 = clientOnly1.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
                        var clientCountingService2 = clientOnly2.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();

                        await clientCountingService1.IncrementAsync();

                        countingService.GetCurrentValue().Should().Be(1);

                        await clientCountingService2.IncrementAsync();

                        countingService.GetCurrentValue().Should().Be(2);
                    }
                }
            }
        }
    }
}
