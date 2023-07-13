
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class WebSocketUsageFixture : BaseTest
    {
#if SUPPORTS_WEB_SOCKET_CLIENT
        [Test]
#endif
        public async Task HalibutSerializerIsKeptUpToDateWithWebSocketPollingTentacle()
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .PollingOverWebSocket()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateClient<IMultipleParametersTestService>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

#if SUPPORTS_WEB_SOCKET_CLIENT
        [Test]
#endif
        public async Task OctopusCanSendMessagesToWebSocketPollingTentacle()
        {

            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .PollingOverWebSocket()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {

                var svc = clientAndService.CreateClient<IMultipleParametersTestService>();
                for (var i = 1; i < 100; i++)
                {
                    var i1 = i;
                    svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                }
            }
        }

    }
}

