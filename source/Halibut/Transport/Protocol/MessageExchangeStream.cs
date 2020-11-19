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
using Halibut.Util;
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
        static readonly string[] AllControlMessages = {MxClient, Next, Proceed, End, MxSubscriber, MxServer};
        static readonly int NumCharsRequiredToIdentifyAControlMessage = AllControlMessages.Max(n => n.Length);
        static readonly int MaxBsonBytesToCapture = 8192;
        readonly Stream stream;
        readonly ILog log;
        readonly StreamWriter streamWriter;
        readonly StreamReader streamReader;
        readonly JsonSerializer serializer;
        readonly Version currentVersion = new Version(1, 0);

        readonly EventType ControlMessageLogEventType = EventType.Diagnostic;
        readonly EventType PayloadMessageLogEventType = EventType.Diagnostic;
        
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

        void SendControlMessage(string controlMessage, bool sendAdditionalNewline)
        {
            streamWriter.Write(controlMessage);
            streamWriter.WriteLine();
            if (sendAdditionalNewline)
            {
                streamWriter.WriteLine();
            }

            streamWriter.Flush();

            if (HalibutLimits.LogControlMessages)
                log.Write(ControlMessageLogEventType, $"Sent: {controlMessage}");
        }

        async Task SendControlMessageAsync(string controlMessage)
        {
            await streamWriter.WriteAsync(controlMessage);
            await streamWriter.WriteLineAsync();
            await streamWriter.FlushAsync();

            if (HalibutLimits.LogControlMessages)
                log.Write(ControlMessageLogEventType, $"Sent: {controlMessage}");
        }
        
        void LogReceivedControlMessage(string controlMessage)
        {
            if (HalibutLimits.LogControlMessages)
                log.Write(ControlMessageLogEventType, $"Received: {controlMessage}");
        }

        public void IdentifyAsClient()
        {
            log.Write(EventType.Diagnostic, "Identifying as a client");
            SendControlMessage($"{MxClient} {currentVersion}", true);
            ExpectServerIdentity();
        }

        public void SendNext()
        {
            SetShortTimeouts();
            SendControlMessage(Next, false);
            SetNormalTimeouts();
        }

        public void SendProceed()
        {
            SendControlMessage(Proceed, false);
        }

        public async Task SendProceedAsync()
        {
            await SendControlMessageAsync(Proceed);
        }

        public void SendEnd()
        {
            SetShortTimeouts();
            SendControlMessage(End, false);
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

            LogReceivedControlMessage(line);

            return line;
        }

        async Task<string> ReadLineAsync()
        {
            var line = await streamReader.ReadLineAsync();
            while (line == string.Empty)
            {
                line = await streamReader.ReadLineAsync();
            }

            LogReceivedControlMessage(line);

            return line;
        }

        public void IdentifyAsSubscriber(string subscriptionId)
        {
            SendControlMessage($"{MxSubscriber} {currentVersion} {subscriptionId}", true);
            ExpectServerIdentity();
        }

        public void IdentifyAsServer()
        {
            SendControlMessage($"{MxServer} {currentVersion}", true);
        }

        public RemoteIdentity ReadRemoteIdentity()
        {
            var line = streamReader.ReadLine();
            if (string.IsNullOrEmpty(line)) throw new ProtocolException("Unable to receive the remote identity; the identity line was empty.");
            
            LogReceivedControlMessage(line);
            
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
            // We have to capture bytes for two reasons:
            // - To check for control messages received at the wrong time.
            // - For logging, if the flag is enabled.
            //
            // We don't capture the entire payload all the time for performance reasons.
            //
            var captureLength = HalibutLimits.LogBsonPayloadBytes
                ? MaxBsonBytesToCapture
                : NumCharsRequiredToIdentifyAControlMessage;
            
            using (var capture = new CaptureReadStream(stream, captureLength))
            {
                try
                {
                    using (var zip = new DeflateStream(capture, CompressionMode.Decompress, true))
                    using (var bson = new BsonDataReader(zip) {CloseInput = false})
                    {
                        var messageEnvelope = serializer.Deserialize<MessageEnvelope>(bson);

                        if (HalibutLimits.LogBsonPayloadBytes)
                            log.Write(PayloadMessageLogEventType, $"Received BSON payload: {BitConverter.ToString(capture.GetBytes())}");

                        if (messageEnvelope == null)
                            throw new Exception("messageEnvelope is null");
                        return (T) messageEnvelope.Message;
                    }
                }
                catch (Exception ex)
                {
                    // Handling a case that can occur when the polling client shut down and sent us a control
                    // message when we expected a BSON payload.

                    var plaintext = SafelyGetPlainText(capture);

                    if (plaintext == null)
                        throw new HalibutClientException($"Data format error: expected deflated bson message, but received unrecognised byte sequence.");
                    
                    if (plaintext.Equals(End))
                        throw new HalibutClientException("Connection ended by remote. This can occur if the remote shut down while a request was in process.");

                    if (LooksLikeAControlMessage(plaintext))
                        throw new HalibutClientException($"Data format error: expected deflated bson message, but got control message '{plaintext}'");

                    log.WriteException(EventType.Error, $"ReadBsonMessage failed to read BSON message: {BitConverter.ToString(capture.GetBytes())}", ex);
                    throw;
                }
            }
        }

        bool LooksLikeAControlMessage(string text)
        {
            return text == End
                   || text == Next
                   || text == Proceed
                   || text.StartsWith(MxClient)
                   || text.StartsWith(MxServer)
                   || text.StartsWith(MxSubscriber);
        }
        
        static string SafelyGetPlainText(CaptureReadStream buffer)
        {
            // A few things here that are important but maybe not obvious:
            // - Use DecoderFallback.ExceptionFallback so that decoding will throw if we find
            //   characters that we can't handle.
            // - Use ASCII rather than UTF-8 because we don't need to support multi-byte chars,
            //   and they could potentially cause issues if we truncate a stream at the wrong
            //   spot.
            
            try
            {
                var bytes = buffer.GetBytes();
                var ascii = Encoding.GetEncoding(
                    "ASCII", 
                    EncoderFallback.ExceptionFallback, 
                    DecoderFallback.ExceptionFallback
                );
                var plaintext = ascii.GetString(bytes, 0, bytes.Length);
                return plaintext.TrimEnd('\r', '\n');
            }
            catch
            {
                // Ok, it's not plaintext
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
            void WriteBsonMessageInternal(Stream writeTo)
            {
                using (var zip = new DeflateStream(writeTo, CompressionMode.Compress, true))
                using (var bson = new BsonDataWriter(zip) { CloseOutput = false })
                {
                    serializer.Serialize(bson, new MessageEnvelope { Message = messages });
                }
            }

            if (HalibutLimits.LogBsonPayloadBytes)
            {
                using (var capture = new CaptureWriteStream(stream, MaxBsonBytesToCapture))
                {
                    WriteBsonMessageInternal(capture);
                    log.Write(EventType.Diagnostic, $"Sent BSON payload: {BitConverter.ToString(capture.GetBytes())}");
                }
            }
            else
            {
                WriteBsonMessageInternal(stream);
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
