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
            log.Write(EventType.Diagnostic, "Identifying as a client");
            streamWriter.Write("MX-CLIENT ");
            streamWriter.Write(currentVersion);
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();
            ExpectServerIdentity();
        }

        public void SendNext()
        {
            SetShortTimeouts();
            streamWriter.Write("NEXT");
            streamWriter.WriteLine();
            streamWriter.Flush();
            SetNormalTimeouts();
        }

        public void SendProceed()
        {
            streamWriter.Write("PROCEED");
            streamWriter.WriteLine();
            streamWriter.Flush();
        }

        public async Task SendProceedAsync()
        {
            await streamWriter.WriteAsync("PROCEED");
            await streamWriter.WriteLineAsync();
            await streamWriter.FlushAsync();
        }

        public void SendEnd()
        {
            SetShortTimeouts();
            streamWriter.Write("END");
            streamWriter.WriteLine();
            streamWriter.Flush();
            SetNormalTimeouts();
        }

        public bool ExpectNextOrEnd()
        {
            var line = ReadLine();
            switch (line)
            {
                case "NEXT":
                    return true;
                case null:
                case "END":
                    return false;
                default:
                    throw new ProtocolException("Expected NEXT or END, got: " + line);
            }
        }

        public async Task<bool> ExpectNextOrEndAsync()
        {
            var line = await ReadLineAsync();
            switch (line)
            {
                case "NEXT":
                    return true;
                case null:
                case "END":
                    return false;
                default:
                    throw new ProtocolException("Expected NEXT or END, got: " + line);
            }
        }

        public void ExpectProceeed()
        {
            SetShortTimeouts();
            var line = ReadLine();
            if (line == null)
                throw new AuthenticationException("XYZ");
            if (line != "PROCEED")
                throw new ProtocolException("Expected PROCEED, got: " + line);
            SetNormalTimeouts();
        }

        string ReadLine()
        {
            var line = streamReader.ReadLine();
            while (line == string.Empty)
            {
                line = streamReader.ReadLine();
            }

            return line;
        }

        async Task<string> ReadLineAsync()
        {
            var line = await streamReader.ReadLineAsync();
            while (line == string.Empty)
            {
                line = await streamReader.ReadLineAsync();
            }

            return line;
        }

        public void IdentifyAsSubscriber(string subscriptionId)
        {
            streamWriter.Write("MX-SUBSCRIBER ");
            streamWriter.Write(currentVersion);
            streamWriter.Write(" ");
            streamWriter.Write(subscriptionId);
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();

            ExpectServerIdentity();
        }

        public void IdentifyAsServer()
        {
            streamWriter.Write("MX-SERVER ");
            streamWriter.Write(currentVersion.ToString());
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();
        }

        public RemoteIdentity ReadRemoteIdentity()
        {
            var line = streamReader.ReadLine();
            if (string.IsNullOrEmpty(line)) throw new ProtocolException("Unable to receive the remote identity; the identity line was empty.");
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var identityType = ParseIdentityType(parts[0]);
            if (identityType == RemoteIdentityType.Subscriber)
            {
                if (parts.Length < 3) throw new ProtocolException("Unable to receive the remote identity; the client identified as a subscriber, but did not supply a subscription ID.");
                var subscriptionId = new Uri(parts[2]);
                return new RemoteIdentity(identityType, subscriptionId);
            }
            return new RemoteIdentity(identityType);
        }

        public void Send<T>(T message)
        {
            using (var capture = StreamCapture.New())
            {
                WriteBsonMessage(message);
                WriteEachStream(capture.SerializedStreams);
            }

            log.Write(EventType.Diagnostic, "Sent: {0}", message);
        }

        public T Receive<T>()
        {
            using (var capture = StreamCapture.New())
            {
                var result = ReadBsonMessage<T>();
                ReadStreams(capture);
                log.Write(EventType.Diagnostic, "Received: {0}", result);
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
                case "MX-CLIENT":
                    return RemoteIdentityType.Client;
                case "MX-SERVER":
                    return RemoteIdentityType.Server;
                case "MX-SUBSCRIBER":
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
            using (var buffer = new BufferedStream(stream, 8192, true))
            using (var zip = new DeflateStream(buffer, CompressionMode.Decompress, true))
            using (var bson = new BsonDataReader(zip) { CloseInput = false })
            {
                return (T)serializer.Deserialize<MessageEnvelope>(bson)?.Message;
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

        static TemporaryFileStream CopyStreamToFile(Guid id, long length, BinaryReader reader)
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
            return new TemporaryFileStream(path);
        }

        static DataStream FindStreamById(StreamCapture capture, Guid id)
        {
            var dataStream = capture.DeserializedStreams.FirstOrDefault(d => d.Id == id);
            if (dataStream == null) throw new Exception("Unexpected stream!");
            return dataStream;
        }

        void WriteBsonMessage<T>(T messages)
        {
            using (var buffer = new BufferedStream(stream, 4096, true))
            using (var zip = new DeflateStream(buffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                serializer.Serialize(bson, new MessageEnvelope { Message = messages });
                bson.Flush();
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
}
