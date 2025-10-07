using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class UsageFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases()]
        public async Task OctopusCanSendMessagesToTentacle_WithEchoService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task LargeMessages(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var random = new Random();
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                for (var i = 1; i < 5; i++)
                {
                    {
                        int length = 1000000 + random.Next(8196);
                        Logger.Information("Sending message of length {Length}", length);
                        var s = Some.RandomAsciiStringOfLength(length);
                        (await echo.SayHelloAsync(s)).Should().Be(s + "...");
                    }
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases()]
        public async Task OctopusCanSendMessagesToTentacle_WithSupportedServices(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var svc = clientAndService.CreateAsyncClient<IMultipleParametersTestService, IAsyncClientMultipleParametersTestService>();
                for (var i = 1; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    {
                        var i1 = i;
                        (await svc.GetLocationAsync(new MapLocation { Latitude = -i, Longitude = i })).Should().Match<MapLocation>(x => x.Latitude == i1 && x.Longitude == -i1);
                    }
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases()]
        public async Task StreamsCanBeSent(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {

                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var count = await echo.CountBytesAsync(DataStream.FromBytes(data));
                    count.Should().Be(1024 * 1024 + 15);
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task SupportsDifferentServiceContractMethods(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IMultipleParametersTestService, IAsyncClientMultipleParametersTestService>();
                await echo.MethodReturningVoidAsync(12, 14);

                (await echo.HelloAsync()).Should().Be("Hello");
                (await echo.HelloAsync("a")).Should().Be("Hello a");
                (await echo.HelloAsync("a", "b")).Should().Be("Hello a b");
                (await echo.HelloAsync("a", "b", "c")).Should().Be("Hello a b c");
                (await echo.HelloAsync("a", "b", "c", "d")).Should().Be("Hello a b c d");
                (await echo.HelloAsync("a", "b", "c", "d", "e")).Should().Be("Hello a b c d e");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f")).Should().Be("Hello a b c d e f");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f", "g")).Should().Be("Hello a b c d e f g");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f", "g", "h")).Should().Be("Hello a b c d e f g h");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f", "g", "h", "i")).Should().Be("Hello a b c d e f g h i");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f", "g", "h", "i", "j")).Should().Be("Hello a b c d e f g h i j");
                (await echo.HelloAsync("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k")).Should().Be("Hello a b c d e f g h i j k");

                (await echo.AddAsync(1, 2)).Should().Be(3);
                (await echo.AddAsync(1.00, 2.00)).Should().Be(3.00);
                (await echo.AddAsync(1.10M, 2.10M)).Should().Be(3.20M);

                (await echo.AmbiguousAsync("a", "b")).Should().Be("Hello string");
                (await echo.AmbiguousAsync("a", new Tuple<string, string>("a", "b"))).Should().Be("Hello tuple");

                var ex = (await AssertionExtensions.Should(() => echo.AmbiguousAsync("a", (string)null!)).ThrowAsync<AmbiguousMethodMatchHalibutClientException>()).And;
                ex.Message.Should().Contain("Ambiguous");

                (await echo.GetLocationAsync(new MapLocation { Latitude = -27, Longitude = 153 })).Should().Match<MapLocation>(x => x.Latitude == 153 && x.Longitude == -27);
            }
        }

        /// <summary>
        /// We don't test with redis, since redis reporting of file progress is done with heart beat messages, which means
        /// we don't get a nice 1-100 number report with lots of hacking with the port forwarder. Progress reporting is tested
        /// else where for redis.
        /// </summary>
        /// <param name="clientAndServiceTestCase"></param>
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, pollingQueuesToTest: PollingQueuesToTest.InMemory)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task StreamsCanBeSentWithProgressReporting(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var progressReported = new List<int>();

                var data = new byte[1024 * 1024 * 16 + 15];
                new Random().NextBytes(data);
                var stream = new MemoryStream(data);

                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                var dataStream = await Task.FromResult(DataStream.FromStream(stream,
                        async (i, token) => {
                            await Task.CompletedTask;
                            progressReported.Add(i);
                        })
                    );
                var count = await echo.CountBytesAsync(dataStream);
                count.Should().Be(1024 * 1024 * 16 + 15);

                progressReported.Should().ContainInOrder(Enumerable.Range(1, 100));
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task OctopusCanSendAndReceiveComplexObjects_WithMultipleDataStreams(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase
                       .CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var service = clientAndService.CreateAsyncClient<IComplexObjectService, IAsyncClientComplexObjectService>();
                var payload1 = "Payload #1";
                var payload2 = "Payload #2";

                for (int i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var request = new ComplexObjectMultipleDataStreams
                    {
                        Payload1 = DataStream.FromString(payload1),
                        Payload2 = DataStream.FromString(payload2),
                    };

                    var response = await service.ProcessAsync(request);

                    response.Payload1.Should().NotBeSameAs(request.Payload1);
                    (await response.Payload1!.ReadAsString(CancellationToken)).Should().Be(payload1);

                    response.Payload2.Should().NotBeSameAs(request.Payload2);
                    (await response.Payload2!.ReadAsString(CancellationToken)).Should().Be(payload2);
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task OctopusCanSendAndReceiveComplexObjects_WithMultipleComplexChildren(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var childPayload1 = "Child Payload #1";
            var childPayload2 = "Child Payload #2";

            var list = new[] { "List Item #1", "List Item #2", "List Item #3" };

            var enumValue = ComplexEnum.RequestValue1;

            var dictionary = new Dictionary<Guid, string>
            {
                { Guid.NewGuid(), "Dictionary #1" },
                { Guid.NewGuid(), "Dictionary #2" },
            };

            var set = new HashSet<ComplexPair<string>>
            {
                new(ComplexEnum.RequestValue1, "ComplexSet #1"),
                new(ComplexEnum.RequestValue3, "ComplexSet #3"),
            };

            await using (var clientAndService = await clientAndServiceTestCase
                       .CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var service = clientAndService.CreateAsyncClient<IComplexObjectService, IAsyncClientComplexObjectService>();
                var request = new ComplexObjectMultipleChildren
                {
                    Child1 = new ComplexChild1
                    {
                        ChildPayload1 = DataStream.FromString(childPayload1),
                        ChildPayload2 = DataStream.FromString(childPayload2),
                        DictionaryPayload = dictionary,
                        ListOfStreams = list.Select(DataStream.FromString).ToList(),
                    },
                    Child2 = new ComplexChild2
                    {
                        EnumPayload = enumValue,
                        ComplexPayloadSet = set.Select(x => new ComplexPair<DataStream>(x.EnumValue, DataStream.FromString(x.Payload))).ToHashSet()
                    }
                };

                var response = await service.ProcessAsync(request);

                response.Child1.Should().NotBeSameAs(request.Child1);
                response.Child1!.ChildPayload1.Should().NotBeSameAs(request.Child1.ChildPayload1);
                (await response.Child1.ChildPayload1!.ReadAsString(CancellationToken)).Should().Be(childPayload1);
                response.Child1.ChildPayload2.Should().NotBeSameAs(request.Child1.ChildPayload2);
                (await response.Child1.ChildPayload2!.ReadAsString(CancellationToken)).Should().Be(childPayload2);
                response.Child1.ListOfStreams.Should().NotBeSameAs(request.Child1.ListOfStreams);
                (await response.Child1.ListOfStreams!.ToAsyncEnumerable()
                    .SelectAwait(async x => await x.ReadAsString(CancellationToken))
                    .ToListAsync(CancellationToken))
                    .Should().BeEquivalentTo(list);
                response.Child1.DictionaryPayload.Should().NotBeSameAs(request.Child1.DictionaryPayload);
                response.Child1.DictionaryPayload.Should().BeEquivalentTo(dictionary);

                response.Child2.Should().NotBeSameAs(request.Child2);
                response.Child2!.EnumPayload.Should().Be(enumValue);
                response.Child2.ComplexPayloadSet.Should().NotBeSameAs(request.Child2.ComplexPayloadSet);
                (await response.Child2.ComplexPayloadSet!.ToAsyncEnumerable()
                    .SelectAwait(async x => new ComplexPair<string>(x.EnumValue, await x.Payload.ReadAsString(CancellationToken)))
                    .ToArrayAsync())
                    .ToHashSet()
                    .Should()
                    .BeEquivalentTo(set);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task OctopusCanSendAndReceiveComplexObjects_WithInheritedChildren(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            const string childPayload1 = "Child Payload #1";
            const string childPayload2 = "Child Payload #2";

            await using var clientAndService = await clientAndServiceTestCase
                .CreateTestCaseBuilder()
                .WithStandardServices()
                .Build(CancellationToken);

            var service = clientAndService.CreateAsyncClient<IComplexObjectService, IAsyncClientComplexObjectService>();

            var request = new ComplexObjectWithInheritance
            {
                Child1 = new ComplexInheritedChild1(childPayload1),
                Child2 = new ComplexInheritedChild2(childPayload2)
            };

            var response = await service.ProcessAsync(request);

            response.Child1.Should().NotBeSameAs(request.Child1);
            response.Child1!.Name.Should().Be(childPayload1);
            response.Child1.Name.Should().Be(request.Child1.Name);

            response.Child2.Should().NotBeSameAs(request.Child2);
            response.Child2!.Description.Should().Be(childPayload2);
            response.Child2.Description.Should().Be(request.Child2.Description);
        }
    }
}
