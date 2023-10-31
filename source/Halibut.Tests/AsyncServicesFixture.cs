using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class AsyncServicesFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task AsyncServiceWithReturnType_CanBeRegisteredAndResolved(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService())
                .Build(CancellationToken);

            var value = Some.RandomAsciiStringOfLength(8);
            var echoServiceClient = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            var result = await echoServiceClient.SayHelloAsync(value);

            result.Should().Be($"{value}Async...");
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task AsyncServiceWithNoReturnType_CanBeRegisteredAndResolved(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithAsyncService<ILockService, IAsyncLockService>(() => new AsyncLockService())
                .Build(CancellationToken);

            string fileToWaitFor = Path.GetTempFileName();
            string fileWhenRequestStarted = Path.GetTempFileName();
            
            File.Delete(fileToWaitFor);

            var lockServiceClient = clientAndService.CreateAsyncClient<ILockService, IAsyncClientLockService>();
            await lockServiceClient.WaitForFileToBeDeletedAsync(fileToWaitFor, fileWhenRequestStarted);

            File.Exists(fileWhenRequestStarted).Should().BeTrue();
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task AsyncServiceWithNoParams_CanBeRegisteredAndResolve(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithAsyncService<ICountingService, IAsyncCountingService>(() => new AsyncCountingService())
                .Build(CancellationToken);

            var countingService = clientAndService.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
            var result = await countingService.IncrementAsync();
            result.Should().Be(1);

        }
    }
}
