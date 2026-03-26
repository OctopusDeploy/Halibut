using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class WhenTransportUsesFlushBufferedStreamFixture : BaseTest
    {
#if !NETFRAMEWORK
        // On net48, SslStream uses APM (BeginWrite/EndWrite) internally during the TLS handshake rather than
        // WriteAsync/FlushAsync. TestOnlySendDataWhenFlushedStream.BeginWrite only writes to an in-memory buffer
        // and never flushes, so handshake data from both sides sits buffered and neither end receives anything —
        // the client times out waiting for the ServerHello. On modern .NET, SslStream calls FlushAsync after each
        // TLS record, so the handshake works correctly. There is no hook to inject a flush between SslStream's
        // internal BeginWrite/EndWrite calls on net48, so this test cannot run there.
        //
        // Why net48's SslStream never calls Flush: it was designed assuming the underlying stream sends data
        // immediately on Write/BeginWrite — a valid assumption for NetworkStream over TCP, where bytes go out
        // without needing an explicit Flush. The net48 implementation treated Flush as a no-op concern and never
        // added the call. When SslStream was rewritten for modern .NET it was made transport-agnostic, explicitly
        // calling FlushAsync after each TLS record so it works correctly with any stream implementation.
        // TestOnlySendDataWhenFlushedStream violates the net48 assumption by holding writes in a MemoryStream
        // until Flush is called, which net48's SslStream never does.
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task RequestsSucceed_WhenStreamsOnlyForwardDataOnFlush(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithClientStreamFactory(new StreamWrappingStreamFactory { WrapStreamWith = s => new TestOnlySendDataWhenFlushedStream(s) })
                .WithServiceStreamFactory(new StreamWrappingStreamFactory { WrapStreamWith = s => new TestOnlySendDataWhenFlushedStream(s) })
                .WithEchoService()
                .Build(CancellationToken);

            var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

            for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
            {
                var result = await echo.SayHelloAsync("hello");
                result.Should().Be("hello...");
            }
        }
    }
#endif
}
