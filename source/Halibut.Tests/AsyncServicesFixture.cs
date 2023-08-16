using System;
using System.IO;
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
        [LatestClientAndLatestServiceTestCases(testAsyncServicesAsWell: true, testSyncService:false, testNetworkConditions: false)]
        public async Task AsyncServiceWithReturnType_CanBeRegisteredAndResolved(ClientAndServiceTestCase clientAndServiceTestCase)
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
            var echoServiceClient = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
            var result = await echoServiceClient.SayHelloAsync(value);

            if (clientAndServiceTestCase.ServiceAsyncHalibutFeature.IsEnabled())
            {
                result.Should().Be($"{value}Async...");
            }
            else
            {
                result.Should().Be($"{value}...");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testAsyncServicesAsWell: true, testSyncService: false, testNetworkConditions: false)]
        public async Task AsyncServiceWithNoReturnType_CanBeRegisteredAndResolved(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var clientAndServiceBuilder = clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder();

            if (clientAndServiceTestCase.ServiceAsyncHalibutFeature.IsEnabled())
            {
                clientAndServiceBuilder = clientAndServiceBuilder
                    .WithAsyncService<ILockService, IAsyncLockService>(() => new AsyncLockService());
            }
            else
            {
                clientAndServiceBuilder = clientAndServiceBuilder
                    .WithService<ILockService>(() => new LockService());
            }

            using var clientAndService = await clientAndServiceBuilder.Build(CancellationToken);

            string fileToWaitFor = Path.GetTempFileName();
            string fileWhenRequestStarted = Path.GetTempFileName();
            
            File.Delete(fileToWaitFor);

            var lockServiceClient = clientAndService.CreateClient<ILockService, IAsyncClientLockService>();
            await lockServiceClient.WaitForFileToBeDeletedAsync(fileToWaitFor, fileWhenRequestStarted);

            File.Exists(fileWhenRequestStarted).Should().BeTrue();
        }
    }
}
