using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
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
        readonly Version currentVersion = new Version(1, 0);

        public MessageExchangeStream(Stream stream, ILog log)
        {
            this.stream = stream;
            this.log = log;
            streamWriter = new StreamWriter(stream, new UTF8Encoding(false));
            streamReader = new StreamReader(stream, new UTF8Encoding(false));
            SetNormalTimeouts();
        }

        static int streamCount;
        static JsonSerializerSettings serializerSettings = CreateDefault();

        public async Task IdentifyAsClient()
        {
            log.Write(EventType.Diagnostic, "Identifying as a client");
            await streamWriter.WriteAsync("MX-CLIENT ").ConfigureAwait(false);
            await streamWriter.WriteAsync(currentVersion.ToString()).ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);
            await ExpectServerIdentity().ConfigureAwait(false);
        }

        public async Task SendNext()
        {
            SetShortTimeouts();
            await streamWriter.WriteAsync("NEXT").ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);
            SetNormalTimeouts();
        }

        public async Task SendProceed()
        {
            await streamWriter.WriteAsync("PROCEED").ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);
        }

        public async Task<bool> ExpectNextOrEnd()
        {
            var line = await ReadLine().ConfigureAwait(false);
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

        public async Task ExpectProceeed()
        {
            SetShortTimeouts();
            var line = await ReadLine().ConfigureAwait(false);
            if (line == null)
                throw new AuthenticationException("XYZ");
            if (line != "PROCEED")
                throw new ProtocolException("Expected PROCEED, got: " + line);
            SetNormalTimeouts();
        }

        async Task<string> ReadLine()
        {
            var line = await streamReader.ReadLineAsync().ConfigureAwait(false);
            while (line == string.Empty)
            {
                line = await streamReader.ReadLineAsync().ConfigureAwait(false);
            }

            return line;
        }

        public async Task IdentifyAsSubscriber(string subscriptionId)
        {
            await streamWriter.WriteAsync("MX-SUBSCRIBER ").ConfigureAwait(false);
            await streamWriter.WriteAsync(currentVersion.ToString()).ConfigureAwait(false);
            await streamWriter.WriteAsync(" ").ConfigureAwait(false);
            await streamWriter.WriteAsync(subscriptionId).ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);

            await ExpectServerIdentity().ConfigureAwait(false);
        }

        public async Task IdentifyAsServer()
        {
            await streamWriter.WriteAsync("MX-SERVER ").ConfigureAwait(false);
            await streamWriter.WriteAsync(currentVersion.ToString()).ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            await streamWriter.FlushAsync().ConfigureAwait(false);
        }

        public async Task<RemoteIdentity> ReadRemoteIdentity()
        {
            var line = await streamReader.ReadLineAsync().ConfigureAwait(false);
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

        public async Task Send<T>(T message)
        {
            using (var capture = StreamCapture.New())
            {
                await WriteBsonMessage(message).ConfigureAwait(false);
                await WriteEachStream(capture.SerializedStreams).ConfigureAwait(false);
            }

            log.Write(EventType.Diagnostic, "Sent: {0}", message);
        }

        public async Task<T> Receive<T>()
        {
            using (var capture = StreamCapture.New())
            {
                var result = await ReadBsonMessage<T>().ConfigureAwait(false);
                await ReadStreams(capture).ConfigureAwait(false);
                log.Write(EventType.Diagnostic, "Received: {0}", result);
                return result;
            }
        }

        static JsonSerializerSettings CreateDefault()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ContractResolver = new HalibutContractResolver(),
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
            return settings;
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

        async Task ExpectServerIdentity()
        {
            var identity = await ReadRemoteIdentity().ConfigureAwait(false);
            if (identity.IdentityType != RemoteIdentityType.Server)
                throw new ProtocolException("Expected the remote endpoint to identity as a server. Instead, it identified as: " + identity.IdentityType);
        }

        async Task<T> ReadBsonMessage<T>()
        {
            var buffer = new BufferedStream(stream, 8192, true);
            using (var zip = new DeflateStream(buffer, CompressionMode.Decompress, true))
            using (var bson = new BsonDataReader(zip) {CloseInput = false})
            {
                await bson.ReadAsync().ConfigureAwait(false); // StartObject
                await bson.ReadAsync().ConfigureAwait(false); // PropertyName
                await bson.ReadAsync().ConfigureAwait(false); // Message
                var data = bson.Value as string;
                await bson.ReadAsync().ConfigureAwait(false); // EndObject

                var result = (T) JsonConvert.DeserializeObject<MessageEnvelope>(data, serializerSettings).Message;
                return result;
            }
        }

        async Task ReadStreams(StreamCapture capture)
        {
            var expected = capture.DeserializedStreams.Count;

            for (var i = 0; i < expected; i++)
            {
                await ReadStream(capture).ConfigureAwait(false);
            }
        }

        async Task ReadStream(StreamCapture capture)
        {
            var reader = new BinaryReader(stream);
            var id = new Guid(reader.ReadBytes(16));
            var length = reader.ReadInt64();
            var dataStream = FindStreamById(capture, id);
            var tempFile = await CopyStreamToFile(id, length, reader).ConfigureAwait(false);
            var lengthAgain = reader.ReadInt64();
            if (lengthAgain != length)
            {
                throw new ProtocolException("There was a problem receiving a file stream: the length of the file was expected to be: " + length + " but less data was actually sent. This can happen if the remote party is sending a stream but the stream had already been partially read, or if the stream was being reused between calls.");
            }

            ((IDataStreamInternal)dataStream).SetReceived(tempFile);
        }
        
        static async Task<TemporaryFileStream> CopyStreamToFile(Guid id, long length, BinaryReader reader)
        {
            var path = Path.Combine(Path.GetTempPath(), string.Format("{0}_{1}", id.ToString(), Interlocked.Increment(ref streamCount)));
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[1024 * 128];
                while (length > 0)
                {
                    var read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                    length -= read;
                    await fileStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
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

        async Task WriteBsonMessage<T>(T messages)
        {
            var buffer = new BufferedStream(stream, 4096, true);
            using (var zip = new DeflateStream(buffer, CompressionMode.Compress, true))
            using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
            {
                var data = JsonConvert.SerializeObject(messages, serializerSettings);
                await bson.WriteStartObjectAsync().ConfigureAwait(false);
                await bson.WritePropertyNameAsync("Message").ConfigureAwait(false);
                await bson.WriteValueAsync(data).ConfigureAwait(false);
                await bson.WriteEndObjectAsync().ConfigureAwait(false);
                await bson.FlushAsync().ConfigureAwait(false);
            }
        }

        async Task WriteEachStream(IEnumerable<DataStream> streams)
        {
            foreach (var dataStream in streams)
            {
                var writer = new BinaryWriter(stream);
                writer.Write(dataStream.Id.ToByteArray());
                writer.Write(dataStream.Length);
                await stream.FlushAsync().ConfigureAwait(false);

                await ((IDataStreamInternal)dataStream).Transmit(stream).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                writer.Write(dataStream.Length);
                await stream.FlushAsync().ConfigureAwait(false);
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

            stream.WriteTimeout = (int) HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
            stream.ReadTimeout = (int) HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;
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
