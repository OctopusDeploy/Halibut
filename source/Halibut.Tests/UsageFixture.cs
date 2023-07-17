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
                
                var requestPayload1 = "Payload #1";
                var responsePayload1 = "Response: Payload #1";
                var requestPayload2 = "Payload #2";
                var responsePayload2 = "Response: Payload #2";
                
                var requestChild1Payload1 = "Child #1, Payload #1";
                var responseChild1Payload1 = "Response: Child #1, Payload #1";
                var requestChild1Payload2 = "Child #1, Payload #2";
                var responseChild1Payload2 = "Response: Child #1, Payload #2";
                var requestChild2Payload1 = "Child #2, Payload #1";
                var responseChild2Payload1 = "Response: Child #2, Payload #1";
                var requestChild2Payload2 = "Child #2, Payload #2";
                var responseChild2Payload2 = "Response: Child #2, Payload #2";

                var requestList1 = new[] { "Child #1, List Item #1", "Child #1, List Item #2", "Child #1, List Item #3" };
                var responseList1 = new[] { "Response: Child #1, List Item #1", "Response: Child #1, List Item #2", "Response: Child #1, List Item #3" };
                
                var requestList2 = new[] { "Child #2, List Item #1", "Child #2, List Item #2", "Child #2, List Item #3" };
                var responseList2 = new[] { "Response: Child #2, List Item #1", "Response: Child #2, List Item #2", "Response: Child #2, List Item #3" };

                var requestEnum1 = ComplexRequestEnum.RequestValue1;
                var responseEnum1 = ComplexResponseEnum.ResponseValue1;

                var requestEnum2 = ComplexRequestEnum.RequestValue2;
                var responseEnum2 = ComplexResponseEnum.ResponseValue2;

                var guid_1_1 = Guid.NewGuid();
                var guid_1_2 = Guid.NewGuid();
                var guid_2_1 = Guid.NewGuid();
                var guid_2_2 = Guid.NewGuid();

                var requestDictionary1 = new Dictionary<Guid, string>
                {
                    { guid_1_1, "Child #1, Dictionary #1" },
                    { guid_1_2, "Child #1, Dictionary #2" },
                };
                var responseDictionary1 = new Dictionary<Guid, string>
                {
                    { guid_1_1, "Response: Child #1, Dictionary #1" },
                    { guid_1_2, "Response: Child #1, Dictionary #2" },
                };
                
                var requestDictionary2 = new Dictionary<Guid, string>
                {
                    { guid_2_1, "Child #2, Dictionary #1" },
                    { guid_2_2, "Child #2, Dictionary #2" },
                };
                var responseDictionary2 = new Dictionary<Guid, string>
                {
                    { guid_2_1, "Response: Child #2, Dictionary #1" },
                    { guid_2_2, "Response: Child #2, Dictionary #2" },
                };

                var requestSet1 = new HashSet<ComplexPair<ComplexRequestEnum, string>>
                {
                    new(ComplexRequestEnum.RequestValue1, "Child #1, ComplexSet #1"),
                    new(ComplexRequestEnum.RequestValue3, "Child #1, ComplexSet #3"),
                };
                var responseSet1 = new HashSet<ComplexPair<ComplexResponseEnum, string>>
                {
                    new(ComplexResponseEnum.ResponseValue1, "Response: Child #1, ComplexSet #1"),
                    new(ComplexResponseEnum.ResponseValue3, "Response: Child #1, ComplexSet #3"),
                };

                var requestSet2 = new HashSet<ComplexPair<ComplexRequestEnum, string>>
                {
                    new(ComplexRequestEnum.RequestValue1, "Child #2, ComplexSet #1"),
                    new(ComplexRequestEnum.RequestValue3, "Child #2, ComplexSet #3"),
                };
                var responseSet2 = new HashSet<ComplexPair<ComplexResponseEnum, string>>
                {
                    new(ComplexResponseEnum.ResponseValue1, "Response: Child #2, ComplexSet #1"),
                    new(ComplexResponseEnum.ResponseValue3, "Response: Child #2, ComplexSet #3"),
                };

                for (int i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var response = service.Process(new ComplexRequest
                    {
                        RequestId = requestId,
                        Payload1 = DataStream.FromString(requestPayload1),
                        Payload2 = DataStream.FromString(requestPayload2),
                        Child1 = new ComplexRequestChild
                        {
                            ChildPayload1 = DataStream.FromString(requestChild1Payload1),
                            ChildPayload2 = DataStream.FromString(requestChild1Payload2),
                            ListOfStreams = requestList1.Select(DataStream.FromString).ToList(),
                            EnumPayload = requestEnum1,
                            DictionaryPayload = requestDictionary1,
                            ComplexPayloadSet = requestSet1.Select(x => new ComplexPair<ComplexRequestEnum, DataStream>(x.Item1, DataStream.FromString(x.Item2))).ToHashSet()
                        },
                        Child2 = new ComplexRequestChild
                        {
                            ChildPayload1 = DataStream.FromString(requestChild2Payload1),
                            ChildPayload2 = DataStream.FromString(requestChild2Payload2),
                            ListOfStreams = requestList2.Select(DataStream.FromString).ToList(),
                            EnumPayload = requestEnum2,
                            DictionaryPayload = requestDictionary2,
                            ComplexPayloadSet = requestSet2.Select(x => new ComplexPair<ComplexRequestEnum, DataStream>(x.Item1, DataStream.FromString(x.Item2))).ToHashSet()
                        },
                    });

                    response.RequestId.Should().Be(requestId);

                    response.Payload1.ReadAsString().Should().Be(responsePayload1);
                    response.Payload2.ReadAsString().Should().Be(responsePayload2);
                    response.Child1.ChildPayload1.ReadAsString().Should().Be(responseChild1Payload1);
                    response.Child1.ChildPayload2.ReadAsString().Should().Be(responseChild1Payload2);
                    response.Child1.ListOfStreams.Select(x => x.ReadAsString()).ToList().Should().BeEquivalentTo(responseList1);
                    response.Child1.EnumPayload.Should().Be(responseEnum1);
                    response.Child1.DictionaryPayload.Should().BeEquivalentTo(responseDictionary1);
                    response.Child1.ComplexPayloadSet.Select(x => new ComplexPair<ComplexResponseEnum, string>(x.Item1, x.Item2.ReadAsString())).ToHashSet().Should().BeEquivalentTo(responseSet1);
                    
                    response.Child2.ChildPayload1.ReadAsString().Should().Be(responseChild2Payload1);
                    response.Child2.ChildPayload2.ReadAsString().Should().Be(responseChild2Payload2);
                    response.Child2.ListOfStreams.Select(x => x.ReadAsString()).ToList().Should().BeEquivalentTo(responseList2);
                    response.Child2.EnumPayload.Should().Be(responseEnum2);
                    response.Child2.DictionaryPayload.Should().BeEquivalentTo(responseDictionary2);
                    response.Child2.ComplexPayloadSet.Select(x => new ComplexPair<ComplexResponseEnum, string>(x.Item1, x.Item2.ReadAsString())).ToHashSet().Should().BeEquivalentTo(responseSet2);
                }
            }
        }
    }
}
