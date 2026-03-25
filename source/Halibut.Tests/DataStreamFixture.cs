using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
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
        public async Task AsyncDataStreamsAreUsedWhenInAsync(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.Client.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(clientAndService.GetServiceEndPoint());

                var data = new byte[1337];
                new Random().NextBytes(data);

                for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
                {
                    var count = await echo.CountBytesAsync(new DataStream(
                            data.Length,
                            async (stream, ct) => await stream.WriteByteArrayAsync(data, ct)
                        )
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
            var ds = new DataStream(data.Length, async (stream, ct) => await stream.WriteAsync(data, 0, data.Length, ct));
            using var memoryStream = new MemoryStream();
            await ((IDataStreamInternal) ds).TransmitAsync(memoryStream, CancellationToken);
            memoryStream.ToArray().Should().BeEquivalentTo(data);
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenSendingADataStream_AndWeSendMoreDataThanWeShould_ThenADescriptiveErrorIsLogged(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder()
                           .Build()
                           .Apply(h => h.ThrowOnDataStreamSizeMismatch = false))
                       .RecordingClientLogs(out var clientLogs)
                       .RecordingServiceLogs(out var serviceLogs)
                       .Build(CancellationToken))
            {
                var readDataStreamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                var actualData = new byte[100];
                new Random().NextBytes(actualData);
                
                var maliciousDataStream = new DataStream(10, async (stream, ct) =>
                {
                    await stream.WriteAsync(actualData, 0, actualData.Length, ct);
                });

                await AssertException.Throws<HalibutClientException>(async () => 
                    await readDataStreamService.SendDataAsync(maliciousDataStream));

                var allClientLogs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                allClientLogs.Should().Contain(log => 
                    log.Type == EventType.Error && 
                    log.FormattedMessage.Contains("Data stream size mismatch detected during send") &&
                    log.FormattedMessage.Contains("Declared length: 10") &&
                    log.FormattedMessage.Contains("Actual bytes written: 100"));

                var allServiceLogs = serviceLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                allServiceLogs.Should().Contain(log => 
                    log.Type == EventType.Error && 
                    log.FormattedMessage.Contains("Data stream size mismatch detected") &&
                    log.FormattedMessage.Contains("Message ID:") &&
                    log.FormattedMessage.Contains("Stream ID:") &&
                    log.FormattedMessage.Contains("Expected length: 10") &&
                    log.FormattedMessage.Contains("Total length of all DataStreams"));
                 
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenSendingADataStream_AndWeSendLessDataThanWeShould_ThenADescriptiveErrorIsLogged(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder()
                           .Build()
                           .Apply(h => h.ThrowOnDataStreamSizeMismatch = false))
                       .RecordingClientLogs(out var clientLogs)
                       .RecordingServiceLogs(out var serviceLogs)
                       .Build(CancellationToken))
            {
                var readDataStreamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                var actualData = new byte[10];
                new Random().NextBytes(actualData);
                
                var underSizedDataStream = new DataStream(100, async (stream, ct) =>
                {
                    await stream.WriteAsync(actualData, 0, actualData.Length, ct);
                });

                await AssertException.Throws<HalibutClientException>(async () => 
                    await readDataStreamService.SendDataAsync(underSizedDataStream));

                var allClientLogs = clientLogs.Values.SelectMany(log => log.GetLogs()).ToList();
                allClientLogs.Should().Contain(log => 
                    log.Type == EventType.Error && 
                    log.FormattedMessage.Contains("Data stream size mismatch detected during send") &&
                    log.FormattedMessage.Contains("Declared length: 100") &&
                    log.FormattedMessage.Contains("Actual bytes written: 10"));
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenSendingADataStream_AndWeSendMoreDataThanWeShould_AndThrowOnDataStreamSizeMismatchIsEnabled_ThenItThrows(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                       {
                           ThrowOnDataStreamSizeMismatch = true
                       })
                       .RecordingClientLogs(out var clientLogs)
                       .Build(CancellationToken))
            {
                var readDataStreamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                var actualData = new byte[100];
                new Random().NextBytes(actualData);
                
                var maliciousDataStream = new DataStream(10, async (stream, ct) =>
                {
                    await stream.WriteAsync(actualData, 0, actualData.Length, ct);
                });

                var exception = await AssertException.Throws<HalibutClientException>(async () => 
                    await readDataStreamService.SendDataAsync(maliciousDataStream));

                exception.And.Message.Should().Contain("Data stream size mismatch");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenSendingADataStream_AndWeSendLessDataThanWeShould_AndThrowOnDataStreamSizeMismatchIsEnabled_ThenItThrows(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                       {
                           ThrowOnDataStreamSizeMismatch = true
                       })
                       .RecordingClientLogs(out var clientLogs)
                       .Build(CancellationToken))
            {
                var readDataStreamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                var actualData = new byte[10];
                new Random().NextBytes(actualData);
                
                var underSizedDataStream = new DataStream(100, async (stream, ct) =>
                {
                    await stream.WriteAsync(actualData, 0, actualData.Length, ct);
                });

                var exception = await AssertException.Throws<HalibutClientException>(async () => 
                    await readDataStreamService.SendDataAsync(underSizedDataStream));

                exception.And.Message.Should().Contain("Data stream size mismatch");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task WhenSendingADataStream_AndWeSendLessDataThanWeShould_AndThrowOnDataStreamSizeMismatchIsEnabled_ThenSecondRequestSucceedsQuickly(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimits
                       {
                           ThrowOnDataStreamSizeMismatch = true,
                           TcpClientReceiveResponseTimeout = TimeSpan.FromSeconds(60)
                       })
                       .RecordingClientLogs(out var clientLogs)
                       .Build(CancellationToken))
            {
                var readDataStreamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                var underSizedData = new byte[10];
                new Random().NextBytes(underSizedData);
                
                var underSizedDataStream = new DataStream(100, async (stream, ct) =>
                {
                    await stream.WriteAsync(underSizedData, 0, underSizedData.Length, ct);
                });
                
                await AssertException.Throws<HalibutClientException>(async () => 
                    await readDataStreamService.SendDataAsync(underSizedDataStream));
                
                var stopwatch = Stopwatch.StartNew();
                
                var correctData = new byte[50];
                new Random().NextBytes(correctData);
                var correctDataStream = new DataStream(50, async (stream, ct) =>
                {
                    await stream.WriteAsync(correctData, 0, correctData.Length, ct);
                });

                var received = await readDataStreamService.SendDataAsync(correctDataStream);
                
                var secondRequestTime = stopwatch.Elapsed;
                received.Should().Be(50);
                secondRequestTime.Should().BeLessThan(TimeSpan.FromSeconds(10), 
                    "second request with correct stream should complete quickly, since the sender" +
                    " detects an issue it will close the connection which will result in the receiver" +
                    " seeing an EOF which results in it entering into a reconnect." +
                    " Previously the sender would need to wait 60s before reconnecting.");
            }
        }
    }
}