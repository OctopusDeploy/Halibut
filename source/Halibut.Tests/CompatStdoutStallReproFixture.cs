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
    public class CompatStdoutStallReproFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task EchoUntilCompatStdoutFills(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .WithStandardServices()
                .Build(CancellationToken);

            var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
            var payload = new string('x', 1024);
            for (var i = 0; i < 2000; i++)
            {
                Logger.Information("REPRO: echo {I}", i);
                (await echo.SayHelloAsync($"{i} {payload}")).Should().Be($"{i} {payload}...");
            }
        }
    }
}
