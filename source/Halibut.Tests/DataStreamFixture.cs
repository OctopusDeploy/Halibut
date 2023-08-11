using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class DataStreamFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [Obsolete]
        public async Task SyncDataStreamWriterCanStillBeUsed(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();

                var data = new byte[1337];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    // Note that the DataStream is only given a sync writer.
                    var count = await echo.CountBytesAsync(new DataStream(data.Length, stream => stream.Write(data, 0, data.Length)));
                    count.Should().Be(data.Length);
                }
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncClients: false)]
        public async Task AsyncDataStreamsAreUsedWhenInAsync(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithForcingClientProxyType(ForceClientProxyType.AsyncClient)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.Client.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(clientAndService.GetServiceEndPoint());

                var data = new byte[1337];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var count = await echo.CountBytesAsync(new DataStream(data.Length, 
                        _ => throw new Exception("You have no power here Gandalf the sync"), 
                        async (stream, ct) => await stream.WriteByteArrayAsync(data, ct)
                        )
                    );
                    count.Should().Be(data.Length);
                }
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task AsyncDataStreamsCanBeUsedInSyncAndAsync(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();

                var data = new byte[1337];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    // Note this only has a async writer.
                    var count = await echo.CountBytesAsync(new DataStream(data.Length,
                            async (stream, ct) => await stream.WriteByteArrayAsync(data, ct))
                    );
                    count.Should().Be(data.Length);
                }
            }
        }

        [Test]
        public async Task ASyncDataStreamWriter_CanBeUsedInAsync()
        {
            var data = new byte[1337];
            new Random().NextBytes(data);
            var ds = new DataStream(data.Length, stream => stream.Write(data, 0, data.Length));
            using var memoryStream = new MemoryStream();
            await ((IDataStreamInternal) ds).TransmitAsync(memoryStream, CancellationToken);
            memoryStream.ToArray().Should().BeEquivalentTo(data);
        }
    }
}