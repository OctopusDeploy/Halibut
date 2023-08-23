using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class AsyncClientAndServiceMustNotUseSyncNetworkIO : BaseTest
    {
        [Test]
        // We are testing we only have async IO so only test async client with an async service.
        // We already know a sync service/client does sync IO.
        [LatestClientAndLatestServiceTestCases(testSyncService: false, testAsyncServicesAsWell: true,
            testSyncClients: false, testAsyncClients: true,
            // TODO: ASYNC ME UP!
            // WebSockets are not yet supported since they will need a special implementation of WebSocketStream for this to work.
            testWebSocket: false)]
        public async Task AsyncClientAndServiceMustNotUseSyncNetworkIOTest(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var syncIoRecordingStreamFactory = new SyncIoRecordingStreamFactory(AsyncHalibutFeature.Enabled);

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithStreamFactory(syncIoRecordingStreamFactory)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                (await echo.SayHelloAsync("Deploy package A")).Should().Be("Deploy package A...");

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    (await echo.SayHelloAsync($"Deploy package A {i}")).Should().Be($"Deploy package A {i}...");
                }
            }

            var distinctPlaces = syncIoRecordingStreamFactory.PlacesSyncIoWasUsed()
                .Select(n =>
                {
                    var lines = n.ToString().Split('\n')
                        .Where(s => !s.ToString().Contains("System.Threading.Tasks"))
                        .Where(s => !s.ToString().Contains("AsyncTaskMethodBuilder"))
                        .Where(s => !s.ToString().Contains("System.Runtime.CompilerServices."))
                        .Where(s => !s.ToString().Contains("System.Threading."))
                        .ToArray();
                    return string.Join("\n", lines);
                })
                .Distinct()
                .ToArray();

            foreach (var distinctPlace in distinctPlaces) Logger.Information("Found sync usage: " + distinctPlace);

            Logger.Information($"{syncIoRecordingStreamFactory.PlacesSyncIoWasUsed().Count} vs distinct {distinctPlaces.Length}");

            // TODO: ASYNC ME UP!
            // We should not exclude anything
            var placesWeAssertOn = distinctPlaces
                // .Where(s => !s.Contains(".Dispose()"))
                // .Where(s => !s.Contains(".Close()"))
                // .Where(s => !s.Contains("Halibut.Transport.RewindableBufferStream.Flush()"))
                // .Where(s => !s.Contains("System.Net.Security.SslStream.Flush()"))
                // .Where(s => !s.Contains("Flush()")) // In TeamCity we seem to have stackless flush calls!
#if NETFRAMEWORK
                .Where(s => !s.Contains("System.Net.Security.SslStream.Flush()"))
                .Where(s => !s.Contains("Halibut.Transport.Streams.ReadIntoMemoryBufferStream.Read"))
#endif
                .ToArray();

            foreach (var s in placesWeAssertOn) Logger.Error("Regression: " + s);

            placesWeAssertOn.Length.Should().Be(0);
            syncIoRecordingStreamFactory.streams.Count.Should().BeGreaterOrEqualTo(2, "Since we should wrap a stream on the client and the service.");
        }
    }
}