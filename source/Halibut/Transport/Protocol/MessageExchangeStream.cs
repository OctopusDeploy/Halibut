using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;

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

        readonly Stream stream;
        readonly ILog log;
        readonly StreamWriter streamWriter;
        readonly IMessageSerializer serializer;
        readonly Version currentVersion = new Version(1, 0);
        readonly ControlMessageReader controlMessageReader = new ControlMessageReader();

        public MessageExchangeStream(Stream stream, IMessageSerializer serializer, ILog log)
        {
            #if NETFRAMEWORK
            this.stream = stream;
            #else
            this.stream = new RewindableBufferStream(stream, HalibutLimits.RewindableBufferStreamSize);
            #endif
            this.log = log;
            streamWriter = new StreamWriter(this.stream, new UTF8Encoding(false)) { NewLine = "\r\n" };
            this.serializer = serializer;
            SetNormalTimeouts();
        }

        static int streamCount;

        public void IdentifyAsClient()
        {
            log.Write(EventType.Diagnostic, "Identifying as a client");
            SendIdentityMessage($"{MxClient} {currentVersion}");
            ExpectServerIdentity();
        }

        void SendControlMessage(string message)
        {
            streamWriter.WriteLine(message);
            streamWriter.Flush();
        }

        async Task SendControlMessageAsync(string message)
        {
            await streamWriter.WriteLineAsync(message).ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);
        }

        void SendIdentityMessage(string identityLine)
        {
            streamWriter.WriteLine(identityLine);
            streamWriter.WriteLine();
            streamWriter.Flush();
        }

        public void SendNext()
        {
            SetShortTimeouts();
            SendControlMessage(Next);
            SetNormalTimeouts();
        }

        public void SendProceed()
        {
            SendControlMessage(Proceed);
        }

        public async Task SendProceedAsync()
        {
            await SendControlMessageAsync(Proceed).ConfigureAwait(false);
        }

        public void SendEnd()
        {
            SetShortTimeouts();
            SendControlMessage(End);
            SetNormalTimeouts();
        }

        public bool ExpectNextOrEnd()
        {
            var line = controlMessageReader.ReadUntilNonEmptyControlMessage(stream);
            switch (line)
            {
                case Next:
                    return true;
                case null:
                case End:
                    return false;
                default:
                    throw new ProtocolException($"Expected {Next} or {End}, got: " + line);
            }
        }

        public async Task<bool> ExpectNextOrEndAsync()
        {
            var line = await controlMessageReader.ReadUntilNonEmptyControlMessageAsync(stream).ConfigureAwait(false);
            switch (line)
            {
                case Next:
                    return true;
                case null:
                case End:
                    return false;
                default:
                    throw new ProtocolException($"Expected {Next} or {End}, got: " + line);
            }
        }

        public void ExpectProceeed()
        {
            SetShortTimeouts();
            var line = controlMessageReader.ReadUntilNonEmptyControlMessage(stream);
            if (line == null)
                throw new AuthenticationException("XYZ");
            if (line != Proceed)
                throw new ProtocolException($"Expected {Proceed}, got: " + line);
            SetNormalTimeouts();
        }

        

        public void IdentifyAsSubscriber(string subscriptionId)
        {
            SendIdentityMessage($"{MxSubscriber} {currentVersion} {subscriptionId}");
            ExpectServerIdentity();
        }

        public void IdentifyAsServer()
        {
            SendIdentityMessage($"{MxServer} {currentVersion}");
        }

        public RemoteIdentity ReadRemoteIdentity()
        {
            var line = controlMessageReader.ReadControlMessage(stream);
            
            
            if (string.IsNullOrEmpty(line)) throw new ProtocolException("Unable to receive the remote identity; the identity line was empty.");
            
            var emptyLine = controlMessageReader.ReadControlMessage(stream);
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
                    if (parts.Length < 3) throw new ProtocolException("Unable to receive the remote identity; the client identified as a subscriber, but did not supply a subscription ID.");
                    var subscriptionId = new Uri(parts[2]);
                    return new RemoteIdentity(identityType, subscriptionId);
                }
                return new RemoteIdentity(identityType);
            }
            catch (ProtocolException)
            {
                log.Write(EventType.Error, "Response:");
                log.Write(EventType.Error, line);
                log.Write(EventType.Error, new StreamReader(stream, new UTF8Encoding(false)).ReadToEnd());

                throw;
            }
        }

        public void Send<T>(T message)
        {
            using (var capture = StreamCapture.New())
            {
                serializer.WriteMessage(stream, message);
                WriteEachStream(capture.SerializedStreams);
            }

            log.Write(EventType.Diagnostic, "Sent: {0}", message);
        }

        public async Task<T> Receive<T>()
        {
            using (var capture = StreamCapture.New())
            {
                var result = await serializer.ReadMessage<T>(stream);
                ReadStreams(capture);
                log.Write(EventType.Diagnostic, "Received: {0}", result);
                return result;
            }
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

        void ExpectServerIdentity()
        {
            var identity = ReadRemoteIdentity();
            if (identity.IdentityType != RemoteIdentityType.Server)
                throw new ProtocolException("Expected the remote endpoint to identity as a server. Instead, it identified as: " + identity.IdentityType);
        }

        void ReadStreams(StreamCapture capture)
        {
            var expected = capture.DeserializedStreams.Count;

            for (var i = 0; i < expected; i++)
            {
                ReadStream(capture);
            }
        }

        void ReadStream(StreamCapture capture)
        {
            var reader = new BinaryReader(stream);
            var id = new Guid(reader.ReadBytes(16));
            var length = reader.ReadInt64();
            var dataStream = FindStreamById(capture, id);
            var tempFile = CopyStreamToFile(id, length, reader);
            var lengthAgain = reader.ReadInt64();
            if (lengthAgain != length)
            {
                throw new ProtocolException("There was a problem receiving a file stream: the length of the file was expected to be: " + length + " but less data was actually sent. This can happen if the remote party is sending a stream but the stream had already been partially read, or if the stream was being reused between calls.");
            }

            ((IDataStreamInternal)dataStream).Received(tempFile);
        }

        TemporaryFileStream CopyStreamToFile(Guid id, long length, BinaryReader reader)
        {
            var path = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", id.ToString(), Interlocked.Increment(ref streamCount)));
            long bytesLeftToRead = length;
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[1024 * 128];
                while (bytesLeftToRead > 0)
                {
                    var read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesLeftToRead));
                    if (read == 0) throw new ProtocolException($"Stream with length {length} was closed after only reading {length - bytesLeftToRead} bytes.");
                    bytesLeftToRead -= read;
                    fileStream.Write(buffer, 0, read);
                }
            }
            return new TemporaryFileStream(path, log);
        }

        static DataStream FindStreamById(StreamCapture capture, Guid id)
        {
            var dataStream = capture.DeserializedStreams.FirstOrDefault(d => d.Id == id);
            if (dataStream == null) throw new Exception("Unexpected stream!");
            return dataStream;
        }

        void WriteEachStream(IEnumerable<DataStream> streams)
        {
            foreach (var dataStream in streams)
            {
                var writer = new BinaryWriter(stream);
                writer.Write(dataStream.Id.ToByteArray());
                writer.Write(dataStream.Length);
                writer.Flush();

                ((IDataStreamInternal)dataStream).Transmit(stream);
                stream.Flush();

                writer.Write(dataStream.Length);
                writer.Flush();
            }
        }

        void SetNormalTimeouts()
        {
            if (!stream.CanTimeout)
                return;

            stream.WriteTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
            stream.ReadTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;
        }

        void SetShortTimeouts()
        {
            if (!stream.CanTimeout)
                return;

            stream.WriteTimeout = (int)HalibutLimits.TcpClientHeartbeatSendTimeout.TotalMilliseconds;
            stream.ReadTimeout = (int)HalibutLimits.TcpClientHeartbeatReceiveTimeout.TotalMilliseconds;
        }
    }
}
