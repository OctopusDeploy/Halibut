using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class UsageFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases]
        public async Task OctopusCanSendMessagesToTentacle_WithEchoService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("Deploy package A").Should().Be("Deploy package A...");

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    echo.SayHello($"Deploy package A {i}").Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases]
        public async Task OctopusCanSendMessagesToTentacle_WithSupportedServices(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var svc = clientAndService.CreateClient<IMultipleParametersTestService>();
                for (var i = 1; i < clientAndServiceTestCase.RecommendedIterations; i++)
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
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .Polling()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                // This is here to exercise the path where the Listener's (web socket) handle loop has the protocol (with type serializer) built before the type is registered
                var echo = clientAndService.CreateClient<IEchoService>();
                // This must come before CreateClient<ISupportedServices> for the situation to occur
                echo.SayHello("Deploy package A").Should().Be("Deploy package A" + "...");

                var svc = clientAndService.CreateClient<IMultipleParametersTestService>();
                // This must happen before the message loop in MessageExchangeProtocol restarts (timeout, exception, or end) for the error to occur
                svc.GetLocation(new MapLocation { Latitude = -27, Longitude = 153 }).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases]
        public async Task StreamsCanBeSent(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task SupportsDifferentServiceContractMethods(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IMultipleParametersTestService>();
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
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task StreamsCanBeSentWithProgressReporting(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
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

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases]
        public async Task OctopusCanSendAndReceiveComplexObjectsWithMultipleDataStreams(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase
                       .CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var service = clientAndService.CreateClient<IComplexObjectService>();
                var requestId = "RequestIdentifier";
                
                var payload1 = "Payload #1";
                var payload2 = "Payload #2";
                
                var child1Payload1 = "Child #1, Payload #1";
                var child1Payload2 = "Child #1, Payload #2";
                var child2Payload1 = "Child #2, Payload #1";
                var child2Payload2 = "Child #2, Payload #2";

                var list1 = new[] { "Child #1, List Item #1", "Child #1, List Item #2", "Child #1, List Item #3" };
                var list2 = new[] { "Child #2, List Item #1", "Child #2, List Item #2", "Child #2, List Item #3" };

                var enum1 = ComplexEnum.RequestValue1;
                var enum2 = ComplexEnum.RequestValue2;

                var child1Guid1 = Guid.NewGuid();
                var child1Guid2 = Guid.NewGuid();
                var child2Guid1 = Guid.NewGuid();
                var child2Guild2 = Guid.NewGuid();

                var dictionary1 = new Dictionary<Guid, string>
                {
                    { child1Guid1, "Child #1, Dictionary #1" },
                    { child1Guid2, "Child #1, Dictionary #2" },
                };
                
                var dictionary2 = new Dictionary<Guid, string>
                {
                    { child2Guid1, "Child #2, Dictionary #1" },
                    { child2Guild2, "Child #2, Dictionary #2" },
                };

                var set1 = new HashSet<ComplexPair<string>>
                {
                    new(ComplexEnum.RequestValue1, "Child #1, ComplexSet #1"),
                    new(ComplexEnum.RequestValue3, "Child #1, ComplexSet #3"),
                };

                var set2 = new HashSet<ComplexPair<string>>
                {
                    new(ComplexEnum.RequestValue1, "Child #2, ComplexSet #1"),
                    new(ComplexEnum.RequestValue3, "Child #2, ComplexSet #3"),
                };

                for (int i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var request = new ComplexRequest
                    {
                        RequestId = requestId,
                        Payload1 = DataStream.FromString(payload1),
                        Payload2 = DataStream.FromString(payload2),
                        Child1 = new ComplexChild
                        {
                            ChildPayload1 = DataStream.FromString(child1Payload1),
                            ChildPayload2 = DataStream.FromString(child1Payload2),
                            ListOfStreams = list1.Select(DataStream.FromString).ToList(),
                            EnumPayload = enum1,
                            DictionaryPayload = dictionary1,
                            ComplexPayloadSet = set1.Select(x => new ComplexPair<DataStream>(x.EnumValue, DataStream.FromString(x.Payload))).ToHashSet()
                        },
                        Child2 = new ComplexChild
                        {
                            ChildPayload1 = DataStream.FromString(child2Payload1),
                            ChildPayload2 = DataStream.FromString(child2Payload2),
                            ListOfStreams = list2.Select(DataStream.FromString).ToList(),
                            EnumPayload = enum2,
                            DictionaryPayload = dictionary2,
                            ComplexPayloadSet = set2.Select(x => new ComplexPair<DataStream>(x.EnumValue, DataStream.FromString(x.Payload))).ToHashSet()
                        },
                    };
                    
                    var response = service.Process(request);

                    response.RequestId.Should().Be(requestId);

                    response.Payload1.Should().NotBeSameAs(request.Payload1);
                    response.Payload1.ReadAsString().Should().Be(payload1);
                    
                    response.Payload2.Should().NotBeSameAs(request.Payload2);
                    response.Payload2.ReadAsString().Should().Be(payload2);

                    response.Child1.Should().NotBeSameAs(request.Child1);
                    response.Child1.ChildPayload1.Should().NotBeSameAs(request.Child1.ChildPayload1);
                    response.Child1.ChildPayload1.ReadAsString().Should().Be(child1Payload1);
                    response.Child1.ChildPayload2.Should().NotBeSameAs(request.Child1.ChildPayload2);
                    response.Child1.ChildPayload2.ReadAsString().Should().Be(child1Payload2);
                    response.Child1.ListOfStreams.Should().NotBeSameAs(request.Child1.ListOfStreams);
                    response.Child1.ListOfStreams.Select(x => x.ReadAsString()).ToList().Should().BeEquivalentTo(list1);
                    response.Child1.EnumPayload.Should().Be(enum1);
                    response.Child1.DictionaryPayload.Should().NotBeSameAs(request.Child1.DictionaryPayload);
                    response.Child1.DictionaryPayload.Should().BeEquivalentTo(dictionary1);
                    response.Child1.ComplexPayloadSet.Should().NotBeSameAs(request.Child1.ComplexPayloadSet);
                    response.Child1.ComplexPayloadSet.Select(x => new ComplexPair<string>(x.EnumValue, x.Payload.ReadAsString())).ToHashSet().Should().BeEquivalentTo(set1);

                    response.Child2.Should().NotBeSameAs(request.Child2);
                    response.Child2.ChildPayload1.Should().NotBeSameAs(request.Child2.ChildPayload1);
                    response.Child2.ChildPayload1.ReadAsString().Should().Be(child2Payload1);
                    response.Child2.ChildPayload2.Should().NotBeSameAs(request.Child2.ChildPayload2);
                    response.Child2.ChildPayload2.ReadAsString().Should().Be(child2Payload2);
                    response.Child2.ListOfStreams.Should().NotBeSameAs(request.Child2.ListOfStreams);
                    response.Child2.ListOfStreams.Select(x => x.ReadAsString()).ToList().Should().BeEquivalentTo(list2);
                    response.Child2.EnumPayload.Should().Be(enum2);
                    response.Child2.DictionaryPayload.Should().NotBeSameAs(request.Child2.DictionaryPayload);
                    response.Child2.DictionaryPayload.Should().BeEquivalentTo(dictionary2);
                    response.Child2.ComplexPayloadSet.Should().NotBeSameAs(request.Child2.ComplexPayloadSet);
                    response.Child2.ComplexPayloadSet.Select(x => new ComplexPair<string>(x.EnumValue, x.Payload.ReadAsString())).ToHashSet().Should().BeEquivalentTo(set2);
                }
            }
        }
    }
}
