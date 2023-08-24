using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams.SynIoRecording;
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
            testNetworkConditions: false)]
        public async Task AsyncClientAndServiceMustNotUseSyncNetworkIOTest(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var syncIoRecordingStreamFactory = new SyncIoRecordingStreamFactory(AsyncHalibutFeature.Enabled);

            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithStandardServices()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithStreamFactory(syncIoRecordingStreamFactory)
                             .Build(CancellationToken))
            {
                var service = clientAndService.CreateClient<IComplexObjectService, IAsyncClientComplexObjectService>();
                var payload1 = "Payload #1";
                var payload2 = "Payload #2";

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var request = new ComplexObjectMultipleDataStreams
                    {
                        Payload1 = DataStream.FromString(payload1),
                        Payload2 = DataStream.FromString(payload2)
                    };

                    var response = await service.ProcessAsync(request);

                    response.Payload1.Should().NotBeSameAs(request.Payload1);
                    response.Payload1.ReadAsString().Should().Be(payload1);

                    response.Payload2.Should().NotBeSameAs(request.Payload2);
                    response.Payload2.ReadAsString().Should().Be(payload2);
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

            var placesWeAssertOn = distinctPlaces
#if NETFRAMEWORK
                // TODO: ASYNC ME UP!
                // It is not clear why ssl streams continue to have flush called on them, all the time.
                // On investigation, its almost as if something is just continuously firing Task.Run(() => sslStream.flush()).
                .Where(s => !s.Contains("System.Net.Security.SslStream.Flush()"))

                // TODO: ASYNC ME UP!
                // All we see in this stack trace is the single call to this method, we see no parent.
                .Where(s => !s.Contains("SyncIoRecordingWebSocketStream.Flush()"))

                // TODO: ASYNC ME UP!
                // We are missing an async dispose on web sockets, and ExecuteRequest in SecureWebSocketListener needs to call the async close.
                .Where(s => !(s.Contains("SynIoRecording.SyncIoRecordingWebSocketStream.Dispose")
                    && s.Contains("ExecuteRequest")))
                
                // TODO: ASYNC ME UP!
                // SecureConnection should dispose websockets async
                .Where(s => !(s.Contains("SynIoRecording.SyncIoRecordingWebSocketStream.Dispose")
                              && s.Contains("Halibut.Transport.SecureConnection.DisposeAsync()")))

                // TODO: ASYNC ME UP!
                // We seem to be using a sync dispose on DisposableNotifierConnection which results in a sync write or close
                .Where(s => !(s.Contains("Halibut.Transport.ConnectionManagerAsync.DisposableNotifierConnection.Dispose()")
                              && s.Contains("SynIoRecording.SyncIoRecordingWebSocketStream")))

                // The follow can not be fixed up
                // SslStream in net48 does not have async dispose, 
                .Where(s => !s.Contains("System.Net.Security.SslStream.Dispose("))
#endif
                .ToArray();

            foreach (var s in placesWeAssertOn) Logger.Error("Regression: " + s);

            placesWeAssertOn.Length.Should().Be(0);
            syncIoRecordingStreamFactory.streams.Count.Should().BeGreaterOrEqualTo(2, "Since we should wrap a stream on the client and the service.");
        }
    }
}