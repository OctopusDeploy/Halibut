using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Protocol
{
    public class MessageExchangeStream : IMessageExchangeStream
    {
        const string Next = "NEXT";
        const string Proceed = "PROCEED";
        const string End = "END";
        const string MxClient = "MX-CLIENT";
        const string MxSubscriber = "MX-SUBSCRIBER";
        const string MxServer = "MX-SERVER";

        readonly RewindableBufferStream stream;
        readonly ILog log;
        readonly IMessageSerializer serializer;
        readonly Version currentVersion = new(1, 0);
        readonly ControlMessageReader controlMessageReader;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IControlMessageObserver controlMessageObserver;

        public MessageExchangeStream(Stream stream, 
            IMessageSerializer serializer, 
            IControlMessageObserver controlMessageObserver, 
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, 
            ILog log)
        {
            this.stream = new RewindableBufferStream(stream, halibutTimeoutsAndLimits.RewindableBufferStreamSize);
            
            this.log = log;
            this.controlMessageObserver = controlMessageObserver;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.controlMessageReader = new ControlMessageReader(controlMessageObserver, halibutTimeoutsAndLimits);
            this.serializer = serializer;

            SetReadAndWriteTimeouts(halibutTimeoutsAndLimits.TcpClientTimeout);
        }

        static int streamCount;

        public async Task IdentifyAsClientAsync(CancellationToken cancellationToken)
        {
            log.Write(EventType.Diagnostic, "Identifying as a client");
            await SendIdentityMessageAsync($"{MxClient} {currentVersion}", cancellationToken);
            await ExpectServerIdentityAsync(cancellationToken);
        }

        async Task SendControlMessageAsync(string message, CancellationToken cancellationToken)
        {
            controlMessageObserver.BeforeSendingControlMessage(message);
            await stream.WriteControlLineAsync(message, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            controlMessageObserver.FinishSendingControlMessage(message);
        }

        async Task SendIdentityMessageAsync(string identityLine, CancellationToken cancellationToken)
        {
            // The identity line and the additional empty line must be sent together as a single write operation when using a stream to mimic the 
            // buffering behaviour of the StreamWriter. When sent as 2 writes to the Stream, old Halibut Services e.g. 4.4.8 will often fail when reading the identity line.
            await stream.WriteControlLineAsync(identityLine + StreamExtensionMethods.ControlMessageNewLine, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        public async Task SendNextAsync(CancellationToken cancellationToken)
        {
            await WithTimeout(
                halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout,
                async () => await SendControlMessageAsync(Next, cancellationToken));
        }

        public async Task SendProceedAsync(CancellationToken cancellationToken)
        {
            var sendTimeout = this.stream.GetReadAndWriteTimeouts();
            if (halibutTimeoutsAndLimits.TcpClientHeartbeatTimeoutShouldActuallyBeUsed)
            {
                sendTimeout = halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout;
            }
            
            await WithTimeout(
                sendTimeout,
                async () => await SendControlMessageAsync(Proceed, cancellationToken));
        }

        public async Task SendEndAsync(CancellationToken cancellationToken)
        {
            await WithTimeout(
                halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout,
                async () => await SendControlMessageAsync(End, cancellationToken));
        }

        public async Task<bool> ExpectNextOrEndAsync(TimeSpan readTimeout, CancellationToken cancellationToken)
        {
            var line = await stream.WithReadTimeout(readTimeout, async () => await controlMessageReader.ReadUntilNonEmptyControlMessageAsync(stream, cancellationToken));
    
            return line switch
            {
                Next => true,
                null => false,
                End => false,
                _ => throw new ProtocolException($"Expected {Next} or {End}, got: " + line)
            };
        }

        public async Task ExpectProceedAsync(CancellationToken cancellationToken)
        {
            await WithTimeout(
                halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout,
                async () =>
                {
                    var line = await controlMessageReader.ReadUntilNonEmptyControlMessageAsync(stream, cancellationToken);

                    if (line == null)
                    {
                        throw new AuthenticationException($"Expected {Proceed}, got no data");
                    }

                    if (line != Proceed)
                    {
                        throw new ProtocolException($"Expected {Proceed}, got: " + line);
                    }
                });
        }

        public async Task IdentifyAsSubscriberAsync(string subscriptionId, CancellationToken cancellationToken)
        {
            await SendIdentityMessageAsync($"{MxSubscriber} {currentVersion} {subscriptionId}", cancellationToken);
            await ExpectServerIdentityAsync(cancellationToken);
        }

        public async Task IdentifyAsServerAsync(CancellationToken cancellationToken)
        {
            await SendIdentityMessageAsync($"{MxServer} {currentVersion}", cancellationToken);
        }

        public async Task<RemoteIdentity> ReadRemoteIdentityAsync(CancellationToken cancellationToken)
        {
            var line = await controlMessageReader.ReadControlMessageAsync(stream, cancellationToken);
            if (string.IsNullOrEmpty(line))
            {
                throw new ProtocolException("Unable to receive the remote identity; the identity line was empty.");
            }

            var emptyLine = await controlMessageReader.ReadControlMessageAsync(stream, cancellationToken);
            if (emptyLine.Length != 0)
            {
                throw new ProtocolException("Unable to receive the remote identity; the following line was not empty.");
            }

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                var identityType = ParseIdentityType(parts[0]);
                if (identityType == RemoteIdentityType.Subscriber)
                {
                    if (parts.Length < 3)
                    {
                        throw new ProtocolException("Unable to receive the remote identity; the client identified as a subscriber, but did not supply a subscription ID.");
                    }

                    var subscriptionId = new Uri(parts[2]);

                    return new RemoteIdentity(identityType, subscriptionId);
                }
                
                return new RemoteIdentity(identityType);
            }
            catch (ProtocolException)
            {
                log.Write(EventType.Error, "Response:");
                log.Write(EventType.Error, line);

                var remainingStreamData = await new StreamReader(stream, new UTF8Encoding(false)).ReadToEndAsync();
                log.Write(EventType.Error, remainingStreamData);

                throw;
            }
        }

        public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
        {
            var serializedStreams = await serializer.WriteMessageAsync(stream, message, cancellationToken);
            var messageId = (message as IHalibutMessage)?.Id ?? "unknown";
            await WriteEachStreamAsync(messageId, serializedStreams, cancellationToken);
            
            log.Write(EventType.Diagnostic, "Sent message");
        }

        public async Task<RequestMessage?> ReceiveRequestAsync(TimeSpan timeoutForReceivingTheFirstByte, CancellationToken cancellationToken)
        {
            await stream.WithReadTimeout(
                timeoutForReceivingTheFirstByte,
                async () => await stream.WaitForDataToBeAvailableAsync(cancellationToken));
            
            return await ReceiveAsync<RequestMessage>(cancellationToken);
        }

        public async Task<ResponseMessage?> ReceiveResponseAsync(CancellationToken cancellationToken)
        {
            // Wait for data to become available using existing timeouts, then once we have data streaming in, use the smaller timeout (so we do not wait as long if an error happens here).
            await stream.WithReadTimeout(
                halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout,
                async () => await stream.WaitForDataToBeAvailableAsync(cancellationToken));

            return await ReceiveAsync<ResponseMessage>(cancellationToken);
        }

        async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
            where T : IHalibutMessage
        {
            var (result, dataStreams) = await serializer.ReadMessageAsync<T>(stream, cancellationToken);
            await ReadStreamsAsync(result.Id, dataStreams, cancellationToken);
            log.Write(EventType.Diagnostic, "Received Message");
            return result;
        }

        async Task WithTimeout(SendReceiveTimeout? timeout, Func<Task> func)
        {
            await stream.WithTimeout(timeout, func);
        }

        void SetReadAndWriteTimeouts(SendReceiveTimeout timeout)
        {
            stream.SetReadAndWriteTimeouts(timeout);
        }

        static RemoteIdentityType ParseIdentityType(string identityType)
        {
            switch (identityType)
            {
                case MxClient:
                    return RemoteIdentityType.Client;
                case MxServer:
                    return RemoteIdentityType.Server;
                case MxSubscriber:
                    return RemoteIdentityType.Subscriber;
                default:
                    throw new ProtocolException("Unable to process remote identity; unknown identity type: '" + identityType + "'");
            }
        }

        async Task ExpectServerIdentityAsync(CancellationToken cancellationToken)
        {
            var identity = await ReadRemoteIdentityAsync(cancellationToken);

            if (identity.IdentityType != RemoteIdentityType.Server)
            {
                throw new ProtocolException("Expected the remote endpoint to identity as a server. Instead, it identified as: " + identity.IdentityType);
            }
        }

        async Task ReadStreamsAsync(string messageId, IReadOnlyList<DataStream> deserializedStreams, CancellationToken cancellationToken)
        {
            var expected = deserializedStreams.Count;

            for (var i = 0; i < expected; i++)
            {
                await ReadStreamAsync(messageId, deserializedStreams, cancellationToken);
            }
        }

        async Task ReadStreamAsync(string messageId, IReadOnlyList<DataStream> deserializedStreams, CancellationToken cancellationToken)
        {
            
            var id = new Guid(await stream.ReadBytesAsync(16, cancellationToken));
            var length = await stream.ReadInt64Async(cancellationToken);
            var dataStream = FindStreamById(deserializedStreams, id);
            
            var tempFile = await CopyStreamToFileAsync(messageId, id, length, stream, deserializedStreams, cancellationToken);
            
            var lengthAgain = await stream.ReadInt64Async(cancellationToken);
            if (lengthAgain != length)
            {
                log.Write(EventType.Error, "Data stream size mismatch detected. Message ID: {0}, Stream ID: {1}, " +
                                           "Expected length: {2}, Actual length claimed at end: {3}. " +
                                           "Total length of all DataStreams to be sent is {4}", 
                                            messageId, id, length, lengthAgain, deserializedStreams.Select(d => d.Length).Sum());
                throw new ProtocolException($"Data stream size mismatch detected. Message Id: {messageId}, Stream ID: {id}, " +
                                            $"Expected length: {length}, Actual length claimed at end: {lengthAgain}. " +
                                            $"Total length of all DataStreams to be sent is {deserializedStreams.Select(d => d.Length).Sum()}");
            }

            ((IDataStreamInternal)dataStream).Received(tempFile);
        }
        
        async Task<TemporaryFileStream> CopyStreamToFileAsync(string messageId, Guid id, long length, Stream stream, IReadOnlyList<DataStream> deserializedStreams, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", id.ToString(), Interlocked.Increment(ref streamCount)));
            long bytesLeftToRead = length;
            var totalDataStreamLength = deserializedStreams.Select(d => d.Length).Sum();
#if !NETFRAMEWORK
            await
#endif
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[65*1024];
                while (bytesLeftToRead > 0)
                {
                    int read = 0;
                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, bytesLeftToRead), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        log.WriteException(EventType.Error, "Data stream reading failed. Message ID: {0}, Stream ID: {1}, " +
                                                            "Expected length: {2}, Actual bytes read: {3}. " +
                                                            "Total length of all DataStreams to be sent is {4}. Exception: {5}",
                                                    ex,
                                                   messageId, id, length, length - bytesLeftToRead, totalDataStreamLength, ex.Message);
                        throw;
                    }
                    
                    if (read == 0)
                    {
                        var bytesRead = length - bytesLeftToRead;
                        log.Write(EventType.Error, "Data stream reading failed. Message ID: {0}, Stream ID: {1}, " +
                                                   "Expected length: {2}, Actual bytes read: {3}. " +
                                                   "Total length of all DataStreams to be sent is {4}. Exception: Stream closed prematurely.", 
                                                   messageId, id, length, bytesRead, totalDataStreamLength);
                        throw new ProtocolException($"Data stream reading failed. Message Id: {messageId}, Stream ID: {id}, " +
                                                    $"Expected length: {length}, Actual bytes read: {bytesRead}. " +
                                                    $"Total length of all DataStreams to be sent is {totalDataStreamLength}. " +
                                                    $"Stream with length {length} was closed after only reading {bytesRead} bytes.");
                    }
                    
                    bytesLeftToRead -= read;
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                }
            }
            return new TemporaryFileStream(path, log);
        }

        static DataStream FindStreamById(IReadOnlyList<DataStream> deserializedStreams, Guid id)
        {
            var dataStream = deserializedStreams.FirstOrDefault(d => d.Id == id);
            
            if (dataStream is null)
            {
                throw new Exception("Unexpected stream!");
            }

            return dataStream;
        }

        async Task WriteEachStreamAsync(string messageId, IEnumerable<DataStream> streams, CancellationToken cancellationToken)
        {
            var streamsList = streams.ToList();
            var totalDataStreamLength = streamsList.Select(d => d.Length).Sum();
            
            foreach (var dataStream in streamsList)
            {
                await stream.WriteByteArrayAsync(dataStream.Id.ToByteArray(), cancellationToken);
                await stream.WriteLongAsync(dataStream.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                var byteCountingStream = new ByteCountingStream(stream, OnDispose.LeaveInputStreamOpen);
                await ((IDataStreamInternal)dataStream).TransmitAsync(byteCountingStream, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                if (byteCountingStream.BytesWritten != dataStream.Length)
                {
                    log.Write(EventType.Error, "Data stream size mismatch detected during send. Message ID: {0}, Stream ID: {1}, " +
                                               "Declared length: {2}, Actual bytes written: {3}. " +
                                               "Total length of all DataStreams to be sent is {4}",
                                               messageId, dataStream.Id, dataStream.Length, byteCountingStream.BytesWritten, totalDataStreamLength);
                    
                    if (halibutTimeoutsAndLimits.ThrowOnDataStreamSizeMismatch)
                    {
                        throw new ProtocolException($"Data stream size mismatch detected during send. Message Id: {messageId}, Stream ID: {dataStream.Id}, " +
                                                    $"Declared length: {dataStream.Length}, Actual bytes written: {byteCountingStream.BytesWritten}. " +
                                                    $"Total length of all DataStreams to be sent is {totalDataStreamLength}.");
                    }
                }

                await stream.WriteLongAsync(dataStream.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
    }
}
