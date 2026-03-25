using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageExchangeStreamFixture : BaseTest
    {
        [Test]
        public async Task ShouldLogErrorWhenDataStreamSendsSizeMismatchedData()
        {
            var inMemoryLog = new InMemoryLogWriter();
            var memoryStream = new MemoryStream();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            limits.ThrowOnDataStreamSizeMismatch = false;
            
            var messageExchangeStream = new MessageExchangeStream(
                memoryStream,
                serializer,
                new NoOpControlMessageObserver(),
                limits,
                inMemoryLog);

            var actualData = new byte[100];
            new Random().NextBytes(actualData);
            
            var maliciousDataStream = new DataStream(10, async (stream, ct) =>
            {
                await stream.WriteAsync(actualData, 0, actualData.Length, ct);
            });

            var requestMessage = new RequestMessage
            {
                Destination = new ServiceEndPoint(new Uri("https://example.com"), "ABC123", new HalibutTimeoutsAndLimitsForTestsBuilder().Build()),
                MethodName = "Test",
                ServiceName = "TestService",
                Params = new object[] { maliciousDataStream }
            };

            await messageExchangeStream.SendAsync(requestMessage, CancellationToken.None);

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch detected during send") &&
                log.FormattedMessage.Contains("Message ID:") &&
                log.FormattedMessage.Contains("Stream ID:") &&
                log.FormattedMessage.Contains("Declared length: 10") &&
                log.FormattedMessage.Contains("Actual bytes written: 100") &&
                log.FormattedMessage.Contains("Total length of all DataStreams"));
        }

        [Test]
        public async Task ShouldLogErrorWhenDataStreamReceivesSizeMismatchedData()
        {
            var inMemoryLog = new InMemoryLogWriter();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            
            var streamData = new MemoryStream();
            var writer = new BinaryWriter(streamData);
            
            var dataStreamId = Guid.NewGuid();
            writer.Write(dataStreamId.ToByteArray());
            writer.Write((long)10);
            
            var actualData = new byte[10];
            new Random().NextBytes(actualData);
            writer.Write(actualData);
            
            writer.Write((long)100);
            
            streamData.Position = 0;
            
            var messageExchangeStream = new MessageExchangeStream(
                streamData,
                serializer,
                new NoOpControlMessageObserver(),
                new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                inMemoryLog);
            
            var deserializedDataStream = new DataStream();
            typeof(DataStream).GetProperty("Id")!.SetValue(deserializedDataStream, dataStreamId);
            
            Func<Task> act = async () =>
            {
                var method = typeof(MessageExchangeStream).GetMethod("ReadStreamAsync", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                await (Task)method!.Invoke(messageExchangeStream, 
                    new object[] { "test-message-id", new[] { deserializedDataStream }, CancellationToken.None })!;
            };

            await act.Should().ThrowAsync<ProtocolException>()
                .WithMessage("*Data stream size mismatch detected*Message Id: test-message-id*Stream ID: *Expected length: 10*Actual length claimed at end: 100*");

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch detected") &&
                log.FormattedMessage.Contains("Stream ID:") &&
                log.FormattedMessage.Contains("Expected length: 10") &&
                log.FormattedMessage.Contains("Actual length claimed at end: 100") &&
                log.FormattedMessage.Contains("Total length of all DataStreams"));
        }
        
        [Test]
        public async Task ShouldLogErrorWhenDataStreamSendsTooFewBytes()
        {
            var inMemoryLog = new InMemoryLogWriter();
            var memoryStream = new MemoryStream();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            limits.ThrowOnDataStreamSizeMismatch = false;
            
            var messageExchangeStream = new MessageExchangeStream(
                memoryStream,
                serializer,
                new NoOpControlMessageObserver(),
                limits,
                inMemoryLog);

            var actualData = new byte[10];
            new Random().NextBytes(actualData);
            
            var underSizedDataStream = new DataStream(100, async (stream, ct) =>
            {
                await stream.WriteAsync(actualData, 0, actualData.Length, ct);
            });

            var requestMessage = new RequestMessage
            {
                Destination = new ServiceEndPoint(new Uri("https://example.com"), "ABC123", new HalibutTimeoutsAndLimitsForTestsBuilder().Build()),
                MethodName = "Test",
                ServiceName = "TestService",
                Params = new object[] { underSizedDataStream }
            };

            await messageExchangeStream.SendAsync(requestMessage, CancellationToken.None);

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch detected during send") &&
                log.FormattedMessage.Contains("Message ID:") &&
                log.FormattedMessage.Contains("Stream ID:") &&
                log.FormattedMessage.Contains("Declared length: 100") &&
                log.FormattedMessage.Contains("Actual bytes written: 10") &&
                log.FormattedMessage.Contains("Total length of all DataStreams"));
        }

        [Test]
        public async Task ShouldLogErrorWhenDataStreamReceivesTooFewBytes()
        {
            var inMemoryLog = new InMemoryLogWriter();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            
            var streamData = new MemoryStream();
            var writer = new BinaryWriter(streamData);
            
            var dataStreamId = Guid.NewGuid();
            writer.Write(dataStreamId.ToByteArray());
            writer.Write((long)100);
            
            var actualData = new byte[10];
            new Random().NextBytes(actualData);
            writer.Write(actualData);
            
            streamData.Position = 0;
            
            var messageExchangeStream = new MessageExchangeStream(
                streamData,
                serializer,
                new NoOpControlMessageObserver(),
                new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                inMemoryLog);
            
            var deserializedDataStream = new DataStream();
            typeof(DataStream).GetProperty("Id")!.SetValue(deserializedDataStream, dataStreamId);
            
            Func<Task> act = async () =>
            {
                var method = typeof(MessageExchangeStream).GetMethod("ReadStreamAsync", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                await (Task)method!.Invoke(messageExchangeStream, 
                    new object[] { "test-message-id", new[] { deserializedDataStream }, CancellationToken.None })!;
            };

            await act.Should().ThrowAsync<ProtocolException>()
                .WithMessage("*Stream with length 100 was closed after only reading 10 bytes*");

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream reading failed") &&
                log.FormattedMessage.Contains($"Stream ID: {dataStreamId}") &&
                log.FormattedMessage.Contains("Expected length: 100") &&
                log.FormattedMessage.Contains("Actual bytes read: 10"));
        }
        
        [Test]
        public async Task ShouldThrowExceptionWhenConfiguredAndDataStreamSendsSizeMismatch()
        {
            var inMemoryLog = new InMemoryLogWriter();
            var memoryStream = new MemoryStream();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            limits.ThrowOnDataStreamSizeMismatch = true;
            
            var messageExchangeStream = new MessageExchangeStream(
                memoryStream,
                serializer,
                new NoOpControlMessageObserver(),
                limits,
                inMemoryLog);

            var actualData = new byte[100];
            new Random().NextBytes(actualData);
            
            var maliciousDataStream = new DataStream(10, async (stream, ct) =>
            {
                await stream.WriteAsync(actualData, 0, actualData.Length, ct);
            });

            var requestMessage = new RequestMessage
            {
                Destination = new ServiceEndPoint(new Uri("https://example.com"), "ABC123", new HalibutTimeoutsAndLimitsForTestsBuilder().Build()),
                MethodName = "Test",
                ServiceName = "TestService",
                Params = new object[] { maliciousDataStream }
            };

            Func<Task> act = async () => await messageExchangeStream.SendAsync(requestMessage, CancellationToken.None);

            await act.Should().ThrowAsync<ProtocolException>()
                .WithMessage("*Data stream size mismatch detected during send*Stream ID:*Declared length: 10*Actual bytes written: 100*");

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch detected during send") &&
                log.FormattedMessage.Contains("Message ID:") &&
                log.FormattedMessage.Contains("Stream ID:") &&
                log.FormattedMessage.Contains("Declared length: 10") &&
                log.FormattedMessage.Contains("Actual bytes written: 100") &&
                log.FormattedMessage.Contains("Total length of all DataStreams"));
        }

        [Test]
        public async Task ShouldNotLogErrorWhenDataStreamSendsCorrectSize()
        {
            var inMemoryLog = new InMemoryLogWriter();
            var memoryStream = new MemoryStream();
            
            var serializer = new MessageSerializerBuilder(new LogFactory()).Build();
            
            var messageExchangeStream = new MessageExchangeStream(
                memoryStream,
                serializer,
                new NoOpControlMessageObserver(),
                new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
                inMemoryLog);

            var actualData = new byte[10];
            new Random().NextBytes(actualData);
            
            var correctDataStream = new DataStream(10, async (stream, ct) =>
            {
                await stream.WriteAsync(actualData, 0, actualData.Length, ct);
            });

            var requestMessage = new RequestMessage
            {
                Destination = new ServiceEndPoint(new Uri("https://example.com"), "ABC123", new HalibutTimeoutsAndLimitsForTestsBuilder().Build()),
                MethodName = "Test",
                ServiceName = "TestService",
                Params = new object[] { correctDataStream }
            };

            await messageExchangeStream.SendAsync(requestMessage, CancellationToken.None);

            var logs = inMemoryLog.GetLogs();
            logs.Should().NotContain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch"));
        }
    }
}
