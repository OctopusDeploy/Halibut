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
            await WriteEachStreamAsync(serializedStreams, cancellationToken);
            
            // This must be a mem leak!
            log.Write(EventType.Diagnostic, "Sent: {0}", message);
        }
        
        public async Task SendAsync(PreparedRequestMessage preparedRequestMessage, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(preparedRequestMessage.RequestBytes, cancellationToken);
            await WriteEachStreamAsync(preparedRequestMessage.DataStreams, cancellationToken);
            
            log.Write(EventType.Diagnostic, "Sent: {0}", "TODO pass activity ID down");
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

        public async Task<ResponseBytesAndDataStreams?> ReceiveResponseBytesAsync(CancellationToken cancellationToken)
        {
            var (result, dataStreams, compressedMessageBytes) = await serializer.ReadMessageAsync<ResponseMessage>(stream, cancellationToken);
            await ReadStreamsAsync(dataStreams, cancellationToken);
            log.Write(EventType.Diagnostic, "Received: {0}", result); // TODO stop sending the response to logs.
            if (compressedMessageBytes == null) return null;
            return new ResponseBytesAndDataStreams(compressedMessageBytes!, dataStreams.ToList());
        }

        async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
        {
            var (result, dataStreams, compressedMessageBytes) = await serializer.ReadMessageAsync<T>(stream, cancellationToken);
            await ReadStreamsAsync(dataStreams, cancellationToken);
            log.Write(EventType.Diagnostic, "Received: {0}", result); // TODO stop sending the response to logs.
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

        async Task ReadStreamsAsync(IReadOnlyList<DataStream> deserializedStreams, CancellationToken cancellationToken)
        {
            var expected = deserializedStreams.Count;

            for (var i = 0; i < expected; i++)
            {
                await ReadStreamAsync(deserializedStreams, cancellationToken);
            }
        }

        async Task ReadStreamAsync(IReadOnlyList<DataStream> deserializedStreams, CancellationToken cancellationToken)
        {
            
            var id = new Guid(await stream.ReadBytesAsync(16, cancellationToken));
            var length = await stream.ReadInt64Async(cancellationToken);
            var dataStream = FindStreamById(deserializedStreams, id);
            var tempFile = await CopyStreamToFileAsync(id, length, stream, cancellationToken);
            var lengthAgain = await stream.ReadInt64Async(cancellationToken);
            if (lengthAgain != length)
            {
                throw new ProtocolException("There was a problem receiving a file stream: the length of the file was expected to be: " + length + " but less data was actually sent. This can happen if the remote party is sending a stream but the stream had already been partially read, or if the stream was being reused between calls.");
            }

            ((IDataStreamInternal)dataStream).Received(tempFile);
        }
        
        async Task<TemporaryFileStream> CopyStreamToFileAsync(Guid id, long length, Stream stream, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", id.ToString(), Interlocked.Increment(ref streamCount)));
            long bytesLeftToRead = length;
#if !NETFRAMEWORK
            await
#endif
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[65*1024];
                while (bytesLeftToRead > 0)
                {
                    var read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, bytesLeftToRead), cancellationToken);
                    if (read == 0) throw new ProtocolException($"Stream with length {length} was closed after only reading {length - bytesLeftToRead} bytes.");
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

        async Task WriteEachStreamAsync(IEnumerable<DataStream> streams, CancellationToken cancellationToken)
        {
            foreach (var dataStream in streams)
            {
                await stream.WriteByteArrayAsync(dataStream.Id.ToByteArray(), cancellationToken);
                await stream.WriteLongAsync(dataStream.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                await ((IDataStreamInternal)dataStream).TransmitAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                await stream.WriteLongAsync(dataStream.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
    }
}
