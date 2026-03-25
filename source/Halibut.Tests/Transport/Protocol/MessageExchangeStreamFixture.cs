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
            
            var messageExchangeStream = new MessageExchangeStream(
                memoryStream,
                serializer,
                new NoOpControlMessageObserver(),
                new HalibutTimeoutsAndLimitsForTestsBuilder().Build(),
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
                log.FormattedMessage.Contains("Declared length: 10") &&
                log.FormattedMessage.Contains("Actual bytes written: 100"));
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
                    new object[] { new[] { deserializedDataStream }, CancellationToken.None })!;
            };

            await act.Should().ThrowAsync<ProtocolException>()
                .WithMessage("*length of the file was expected to be: 10*");

            var logs = inMemoryLog.GetLogs();
            logs.Should().Contain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch detected") &&
                log.FormattedMessage.Contains("Expected length: 10") &&
                log.FormattedMessage.Contains("Actual length claimed at end: 100"));
        }
        
        [Test]
        public async Task ShouldLogErrorWhenDataStreamSendsTooFewBytes()
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
                log.FormattedMessage.Contains("Declared length: 100") &&
                log.FormattedMessage.Contains("Actual bytes written: 10"));
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
                    new object[] { new[] { deserializedDataStream }, CancellationToken.None })!;
            };

            await act.Should().ThrowAsync<ProtocolException>()
                .WithMessage("*Stream with length 100 was closed after only reading 10 bytes*");

            var logs = inMemoryLog.GetLogs();
            logs.Should().NotContain(log => 
                log.Type == EventType.Error && 
                log.FormattedMessage.Contains("Data stream size mismatch"));
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
