#if SUPPORTS_WEB_SOCKET_CLIENT
using System;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class WebSocketUsageFixture
    {
        [Test]
        public void HalibutSerializerIsKeptUpToDateWithWebSocketPollingTentacle()
        {
            using (var clientAndService = ClientServiceBuilder
                       .PollingOverWebSocket()
                       .WithService<IEchoService>(() => new EchoService())
                       .WithService<ISupportedServices>(() => new SupportedServices())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateClient<ISupportedServices>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        public void OctopusCanSendMessagesToWebSocketPollingTentacle()
        {

            using (var clientAndService = ClientServiceBuilder
                       .PollingOverWebSocket()
                       .WithService<ISupportedServices>(() => new SupportedServices())
                       .Build())
            {

                var svc = clientAndService.CreateClient<ISupportedServices>();
                for (var i = 1; i < 100; i++)
                {
                    var i1 = i;
                    svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                }
            }
        }

    }
}
#endif