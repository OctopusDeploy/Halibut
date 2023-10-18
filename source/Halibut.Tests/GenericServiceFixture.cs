using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class GenericServiceFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task GenericServiceNamesAreCorrectlyCalled(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .WithGenericService<string>()
                .WithGenericService<double>()
                .WithHalibutLoggingLevel(LogLevel.Info)
                .Build(CancellationToken);

            var stringClient = clientAndService.CreateClient<IGenericService<string>, IAsyncClientGenericService<string>>();
            (await stringClient.GetInfoAsync("Cool string")).Should().Be("String => Cool string");

            var doubleClient = clientAndService.CreateClient<IGenericService<double>, IAsyncClientGenericService<double>>();
            (await doubleClient.GetInfoAsync(6.54321)).Should().Be("Double => 6.54321");
        }
    }
}