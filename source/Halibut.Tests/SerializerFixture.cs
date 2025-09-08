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
    public class SerializerFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task HalibutSerializerIsKeptUpToDateWithPollingTentacle(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateAsyncClient<IMultipleParametersTestService, IAsyncClientMultipleParametersTestService>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                (await svc.GetLocationAsync(new MapLocation { Latitude = -27, Longitude = 153 })).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }
    }
}

