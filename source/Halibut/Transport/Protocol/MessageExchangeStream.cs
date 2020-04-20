using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;
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

        public void Send(MessageEnvelope message)
        {
            WriteBsonMessage(message);
            log.Write(EventType.Diagnostic, "Sent: {0}", message);
        }

        public IncomingMessageEnvelope Receive()
        {
            var result = ReadBsonMessage();
            log.Write(EventType.Diagnostic, "Received: {0}", result);
            return result;
        }

        static JsonSerializer CreateDefault()
        {
            var serializer = JsonSerializer.Create();
            serializer.Formatting = Formatting.None;
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

        IncomingMessageEnvelope ReadBsonMessage()
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using(var binaryReader = new BinaryReader(zip, Encoding.UTF8, true))
            {
                var bytes = ReadBson(binaryReader);
                var messageEnvelope = bytes.FromBson<IncomingMessageEnvelope>();
                if (messageEnvelope == null)
                    throw new Exception("messageEnvelope is null");
                messageEnvelope.InternalMessage = bytes;

                return messageEnvelope;
            }
        }

        byte[] ReadBson(BinaryReader binaryReader)
        {
            //read bson, read first 4 bytes for size, then read size minus the 4 bytes to get the complete data.
            var sizeBytes = binaryReader.ReadBytes(4);
            var size = BitConverter.ToInt32(sizeBytes, 0);
            var bytes = sizeBytes.Concat(binaryReader.ReadBytes(size - 4)).ToArray();
            return bytes;
        }

        void WriteBsonMessage(MessageEnvelope messages)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                if (messages is IncomingMessageEnvelope incomingMessageEnvelope)
                {
                    using (var ms = new MemoryStream(incomingMessageEnvelope.InternalMessage))
                    {
                        ms.CopyTo(zip);
                    }
                }
                else if(messages is OutgoingMessageEnvelope outgoingMessageEnvelope)
                {
                    using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
                    {
                        serializer.Serialize(bson, outgoingMessageEnvelope);
                    }
                }
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
