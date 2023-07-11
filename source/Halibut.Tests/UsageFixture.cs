using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests
{
    public class UsageFixture
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task ClientCanSendMessagesToOldTentacle_WithEchoService(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientAndPreviousServiceVersionBuilder
                       .WithService(serviceConnectionType)
                       .WithServiceVersion(PreviousVersions.v5_0_429)
                       .WithPortForwarding(i => PortForwarderUtil.ForwardingToLocalPort(i).Build())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < StandardIterationCount.ForServiceType(serviceConnectionType); i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }
        
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task OctopusCanSendMessagesToTentacle_WithEchoService(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < StandardIterationCount.ForServiceType(serviceConnectionType); i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task OctopusCanSendMessagesToTentacle_WithSupportedServices(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithSupportedServices()
                       .Build())
            {
                var svc = clientAndService.CreateClient<ISupportedServices>();
                for (var i = 1; i < 100; i++)
                {
                    {
                        var i1 = i;
                        svc.GetLocation(new MapLocation { Latitude = -i, Longitude = i }).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                    }
                }
            }
        }

        [Test]
        public async Task HalibutSerializerIsKeptUpToDateWithPollingTentacle()
        {
            using (var clientAndService = await ClientServiceBuilder
                       .Polling()
                       .WithEchoService()
                       .WithSupportedServices()
                       .Build()){

                // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered
                var echo = clientAndService.CreateClient<IEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateClient<ISupportedServices>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task StreamsCanBeSent(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task StreamsCanBeSentWithLatency(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithPortForwarding(octopusPort => PortForwarderUtil.ForwardingToLocalPort(octopusPort).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        [TestCase(ServiceConnectionType.Polling, 1)]
        [TestCase(ServiceConnectionType.Listening, 2)]
        [TestCase(ServiceConnectionType.Polling, 2)]
        [TestCase(ServiceConnectionType.Listening, 3)]
        [TestCase(ServiceConnectionType.Polling, 3)]
        public async Task StreamsCanBeSentWithLatencyAndTheLastNBytesArriveLate(ServiceConnectionType serviceConnectionType, int numberOfBytesToDelaySending)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .WithPortForwarding(octopusPort => PortForwarderUtil.ForwardingToLocalPort(octopusPort)
                           .WithSendDelay(TimeSpan.FromMilliseconds(20))
                           .WithNumberOfBytesToDelaySending(numberOfBytesToDelaySending)
                           .Build())
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task SupportsDifferentServiceContractMethods(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                     .ForMode(serviceConnectionType)
                     .WithEchoService()
                     .WithSupportedServices()
                     .Build())
            {
                var echo = clientAndService.CreateClient<ISupportedServices>();
                echo.MethodReturningVoid(12, 14);

                echo.Hello().Should().Be("Hello");
                echo.Hello("a").Should().Be("Hello a");
                echo.Hello("a", "b").Should().Be("Hello a b");
                echo.Hello("a", "b", "c").Should().Be("Hello a b c");
                echo.Hello("a", "b", "c", "d").Should().Be("Hello a b c d");
                echo.Hello("a", "b", "c", "d", "e").Should().Be("Hello a b c d e");
                echo.Hello("a", "b", "c", "d", "e", "f").Should().Be("Hello a b c d e f");
                echo.Hello("a", "b", "c", "d", "e", "f", "g").Should().Be("Hello a b c d e f g");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h").Should().Be("Hello a b c d e f g h");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i").Should().Be("Hello a b c d e f g h i");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j").Should().Be("Hello a b c d e f g h i j");
                echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k").Should().Be("Hello a b c d e f g h i j k");

                echo.Add(1, 2).Should().Be(3);
                echo.Add(1.00, 2.00).Should().Be(3.00);
                echo.Add(1.10M, 2.10M).Should().Be(3.20M);

                echo.Ambiguous("a", "b").Should().Be("Hello string");
                echo.Ambiguous("a", new Tuple<string, string>("a", "b")).Should().Be("Hello tuple");

                var ex = Assert.Throws<AmbiguousMethodMatchHalibutClientException>(() => echo.Ambiguous("a", (string)null));
                ex.Message.Should().Contain("Ambiguous");

                echo.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task StreamsCanBeSentWithProgressReporting(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .WithEchoService()
                       .Build())
            {
                var progressReported = new List<int>();

                var data = new byte[1024 * 1024 * 16 + 15];
                new Random().NextBytes(data);
                var stream = new MemoryStream(data);

                var echo = clientAndService.CreateClient<IEchoService>();

                var count = echo.CountBytes(DataStream.FromStream(stream, progressReported.Add));
                count.Should().Be(1024 * 1024 * 16 + 15);

                progressReported.Should().ContainInOrder(Enumerable.Range(1, 100));
            }
        }
    }
}
