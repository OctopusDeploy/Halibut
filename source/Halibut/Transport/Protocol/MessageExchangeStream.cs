using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Transport.Protocol
{
    public class MessageExchangeStream : IMessageExchangeStream
    {
        const string MxClient = "MX-CLIENT";
        const string Next = "NEXT";
        const string Proceed = "PROCEED";
        const string End = "END";
        const string MxSubscriber = "MX-SUBSCRIBER";
        const string MxServer = "MX-SERVER";
        static readonly string[] ControlMessages = {MxClient, MxSubscriber, MxServer, Next, Proceed, End};
        readonly Stream stream;
        readonly ILog log;
        readonly StreamWriter streamWriter;
        readonly StreamReader streamReader;
        readonly JsonSerializer serializer;
        readonly Version currentVersion = new Version(1, 0);

        public MessageExchangeStream(Stream stream, ILog log)
        {
            this.stream = stream;
            this.log = log;
            streamWriter = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n" };
            streamReader = new StreamReader(stream, new UTF8Encoding(false));
            serializer = Serializer();
            SetNormalTimeouts();
        }

        static int streamCount = 0;
        public static Func<JsonSerializer> Serializer = CreateDefault;

        public void IdentifyAsClient()
        {
            log.Write(EventType.Diagnostic, $"Sent: {MxClient} {currentVersion}");
            log.Write(EventType.Diagnostic, "Identifying as a client");
            streamWriter.Write($"{MxClient} {currentVersion}");
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();
            ExpectServerIdentity();
        }

        public void SendNext()
        {
            log.Write(EventType.Diagnostic, $"Sent: {Next}");
            SetShortTimeouts();
            streamWriter.Write(Next);
            streamWriter.WriteLine();
            streamWriter.Flush();
            SetNormalTimeouts();
        }

        public void SendProceed()
        {
            log.Write(EventType.Diagnostic, $"Sent: {Proceed}");
            streamWriter.Write(Proceed);
            streamWriter.WriteLine();
            streamWriter.Flush();
        }

        public async Task SendProceedAsync()
        {
            log.Write(EventType.Diagnostic, $"Sent: {Proceed}");
            await streamWriter.WriteAsync(Proceed);
            await streamWriter.WriteLineAsync();
            await streamWriter.FlushAsync();
        }

        public void SendEnd()
        {
            log.Write(EventType.Diagnostic, $"Sent: {End}");

            SetShortTimeouts();
            streamWriter.Write(End);
            streamWriter.WriteLine();
            streamWriter.Flush();
            SetNormalTimeouts();
        }

        public bool ExpectNextOrEnd()
        {
            var line = ReadLine();
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
            var line = await ReadLineAsync();
            switch (line)
            {
                case Next:
                    return true;
                case null:
                case End:
                    return false;
                default:
                    throw new ProtocolException($"Expected {Next} or {End}, got:  " + line);
            }
        }

        public void ExpectProceeed()
        {
            SetShortTimeouts();
            var line = ReadLine();
            if (line == null)
                throw new AuthenticationException("XYZ");
            if (line != Proceed)
                throw new ProtocolException($"Expected {Proceed}, got: " + line);
            SetNormalTimeouts();
        }

        string ReadLine()
        {
            var line = streamReader.ReadLine();
            while (line == string.Empty)
            {
                line = streamReader.ReadLine();
            }
            log.Write(EventType.Diagnostic, "Received: {0}", line);

            return line;
        }

        async Task<string> ReadLineAsync()
        {
            var line = await streamReader.ReadLineAsync();
            while (line == string.Empty)
            {
                line = await streamReader.ReadLineAsync();
            }
            log.Write(EventType.Diagnostic, "Received: {0}", line);

            return line;
        }

        public void IdentifyAsSubscriber(string subscriptionId)
        {
            log.Write(EventType.Diagnostic, $"Sent: {MxSubscriber} {currentVersion} {subscriptionId}");

            streamWriter.Write($"{MxSubscriber} {currentVersion} {subscriptionId}");
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();

            ExpectServerIdentity();
        }

        public void IdentifyAsServer()
        {
            log.Write(EventType.Diagnostic, $"Sent: {MxServer} {currentVersion}");

            streamWriter.Write($"{MxServer} {currentVersion}");
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();
        }

        public RemoteIdentity ReadRemoteIdentity()
        {
            var line = streamReader.ReadLine();
            if (string.IsNullOrEmpty(line)) throw new ProtocolException("Unable to receive the remote identity; the identity line was empty.");
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
                log.Write(EventType.Error, streamReader.ReadToEnd());

                throw;
            }
        }

        public void Send<T>(T message)
        {
            using (var capture = StreamCapture.New())
            {
                WriteBsonMessage(message);
                WriteEachStream(capture.SerializedStreams);
            }

            log.Write(EventType.Diagnostic, "Sent: {0}", JsonConvert.SerializeObject(message));
        }

        public T Receive<T>()
        {
            using (var capture = StreamCapture.New())
            {
                var result = ReadBsonMessage<T>();
                ReadStreams(capture);
                log.Write(EventType.Diagnostic, "Received: {0}", JsonConvert.SerializeObject(result));
                return result;
            }
        }

        static JsonSerializer CreateDefault()
        {
            var serializer = JsonSerializer.Create();
            serializer.Formatting = Formatting.None;
            serializer.ContractResolver = new HalibutContractResolver();
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            serializer.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            serializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            return serializer;
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

        T ReadBsonMessage<T>()
        {
            using (var buffer = new BufferedReadStream(stream))
            {
                try
                {
                    using (var zip = new DeflateStream(buffer, CompressionMode.Decompress, true))
                    {
                        using (var bson = new BsonDataReader(zip) {CloseInput = false})
                        {
                            var messageEnvelope = serializer.Deserialize<MessageEnvelope>(bson);

                            log.Write(EventType.Diagnostic, $"ReadBsonMessage: {BitConverter.ToString(buffer.GetBytes())}");

                            if (messageEnvelope == null)
                                throw new Exception("messageEnvelope is null");
                            return (T) messageEnvelope.Message;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var plaintext = SafelyGetPlainText(buffer);
                    if (plaintext.Equals(End))
                        throw new HalibutClientException("Connection ended by remote.");
                    if (ControlMessages.Contains(plaintext))
                        throw new HalibutClientException($"Data format error: expected deflated bson message, but got control message '{plaintext}'");

                    log.WriteException(EventType.Error, $"ReadBsonMessage failed to read BSON message: {BitConverter.ToString(buffer.GetBytes())}", ex);
                    throw;
                }
            }
        }

        static string SafelyGetPlainText(BufferedReadStream buffer)
        {
            try
            {
                var bytes = buffer.GetBytes();
                var ascii = new ASCIIEncoding();
                var plaintext = ascii.GetString(bytes, 0, Math.Min(bytes.Length, 10));
                return plaintext.TrimEnd('\r', '\n');
            }
            catch
            {
                //ok, it's not plaintext
                return null;
            }
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
            using (var reader = new BinaryReader(stream, new UTF8Encoding(), true))
            {
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
        }

        TemporaryFileStream CopyStreamToFile(Guid id, long length, BinaryReader reader)
        {
            var path = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", id.ToString(), Interlocked.Increment(ref streamCount)));
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[1024 * 128];
                while (length > 0)
                {
                    var read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                    length -= read;
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

        void WriteBsonMessage<T>(T messages)
        {
            using (var buffer = new BufferedWriteStream(stream))
            using (var zip = new DeflateStream(buffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                serializer.Serialize(bson, new MessageEnvelope { Message = messages });

                log.Write(EventType.Diagnostic, $"WriteBsonMessage: {BitConverter.ToString(buffer.GetBytes())}");
            }
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

        class MessageEnvelope
        {
            public object Message { get; set; }
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

    class BufferedReadStream : Stream
    {
        readonly Stream inner;
        readonly List<byte> bytes = new List<byte>();

        public BufferedReadStream(Stream inner)
        {
            this.inner = inner;
        }

        public byte[] GetBytes() => bytes.ToArray();

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = inner.Read(buffer, offset, count);
            bytes.AddRange(buffer.Take(bytesRead));
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer, offset, count);
            throw new NotImplementedException();
        }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
    }

    class BufferedWriteStream : Stream
    {
        readonly Stream inner;

        public BufferedWriteStream(Stream inner)
        {
            this.inner = inner;
        }

        List<byte> bytes = new List<byte>();

        public byte[] GetBytes() => bytes.ToArray();

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            bytes.AddRange(buffer.Skip(offset).Take(count));
            inner.Write(buffer, offset, count);
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
    }
}
