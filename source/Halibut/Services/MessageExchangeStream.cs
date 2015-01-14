using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Security;
using System.Text;
using Halibut.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Services
{
    public class ProtocolVersions
    {
        
    }

    public enum RemoteIdentityType
    {
        Client,
        Subscriber,
        Server
    }

    public class RemoteIdentity
    {
        readonly RemoteIdentityType identityType;
        readonly Uri subscriptionId;

        public RemoteIdentity(RemoteIdentityType identityType, Uri subscriptionId)
        {
            this.identityType = identityType;
            this.subscriptionId = subscriptionId;
        }

        public RemoteIdentity(RemoteIdentityType identityType)
        {
            this.identityType = identityType;
        }

        public RemoteIdentityType IdentityType
        {
            get { return identityType; }
        }

        public Uri SubscriptionId
        {
            get { return subscriptionId; }
        }
    }

    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Implements the core message exchange protocol for both the client and server. 
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly MessageExchangeStream stream;

        public MessageExchangeProtocol(Stream stream)
        {
            this.stream = new MessageExchangeStream(stream);
        }

        public ResponseMessage ExchangeAsClient(RequestMessage request)
        {
            // SEND: MX-CLIENT 1.0
            // RECV: MX-SERVER 1.0
            // SEND: Request
            // RECV: Response

            // TODO: Error handling
            stream.IdentifyAsClient();
            stream.Send(request);
            return stream.Receive<ResponseMessage>();
        }

        public int ExchangeAsSubscriber(Uri subscriptionId, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            // SEND: MX-SUBSCRIBER 1.0 [subid]
            // RECV: MX-SERVER 1.0
            // RECV: Request -> service invoker
            // SEND: Response
            // Repeat while request != null

            // TODO: Error handling
            stream.IdentifyAsSubscriber(subscriptionId.ToString());
            var requestsProcessed = 0;
            while (ReceiveAndProcessRequest(stream, incomingRequestProcessor)) requestsProcessed++;
            return requestsProcessed;
        }

        static bool ReceiveAndProcessRequest(MessageExchangeStream stream, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            // TODO: Error handling
            var request = stream.Receive<RequestMessage>();
            if (request == null) return false;
            var response = incomingRequestProcessor(request);
            stream.Send(response);
            return true;
        }

        public void ExchangeAsServer(Func<RequestMessage, ResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            // RECV: <IDENTIFICATION>
            // SEND: MX-SERVER 1.0
            // IF MX-CLIENT
            //   RECV: Request
            //     call service invoker
            //   SEND: Response
            // ELSE
            //   while not empty
            //     Get next from queue
            //     SEND: Request
            //     RECV: Response

            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequest(incomingRequestProcessor);
                    break;
                case RemoteIdentityType.Subscriber:
                    ProcessSubscriber(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        void ProcessClientRequest(Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            // TODO: Error handling
            var request = stream.Receive<RequestMessage>();
            var response = incomingRequestProcessor(request);
            stream.Send(response);
        }

        void ProcessSubscriber(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                // TODO: Error handling
                var nextRequest = pendingRequests.Dequeue();

                stream.Send(nextRequest);
                if (nextRequest == null) 
                    break;

                var response = stream.Receive<ResponseMessage>();
                pendingRequests.ApplyResponse(response);
            }
        }
    }

    public class MessageExchangeStream
    {
        readonly Stream stream;
        readonly StreamWriter streamWriter;
        readonly StreamReader streamReader;
        readonly JsonSerializer serializer;
        readonly Version currentVersion = new Version(1, 0);

        public MessageExchangeStream(Stream stream)
        {
            this.stream = stream;
            streamWriter = new StreamWriter(stream, new UTF8Encoding(false));
            streamReader = new StreamReader(stream, new UTF8Encoding(false));
            serializer = Serializer();
        }

        public static Func<JsonSerializer> Serializer = CreateDefault;

        public void IdentifyAsClient()
        {
            streamWriter.Write("MX-CLIENT ");
            streamWriter.Write(currentVersion);
            streamWriter.WriteLine();
            streamWriter.WriteLine();
            streamWriter.Flush();

            ExpectServerIdentity();
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
            streamWriter.Write(currentVersion);
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
        }

        public T Receive<T>()
        {
            using (var capture = StreamCapture.New())
            {
                var result = ReadBsonMessage<T>();
                ReadStreams(capture);
                return result;
            }
        }

        //int ReceiveIncomingRequests(Stream stream)
        //{
        //    var inboundRequests = Read<RequestMessage>(stream);
        //    var inboundResponse = inboundRequests == null ? null : serviceInvoker(inboundRequests);
        //    Write(inboundResponse, stream);
        //    return inboundRequests == null ? 0 : 1;
        //}

        //int SendOutgoingRequests(Stream stream)
        //{
        //    var request = queue.Dequeue();
        //    Write(request, stream);
        //    var responses = Read<ResponseMessage>(stream);
        //    queue.ApplyResponse(responses);
        //    return request == null ? 0 : 1;
        //}

        static JsonSerializer CreateDefault()
        {
            var serializer = JsonSerializer.Create();
            serializer.Formatting = Formatting.None;
            serializer.ContractResolver = new HalibutContractResolver();
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            serializer.TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple;
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
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var buffer = new BufferedStream(zip, 8192))
            using (var bson = new BsonReader(buffer) { CloseInput = false })
            {
                return (T)serializer.Deserialize<MessageEnvelope>(bson).Message;
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
            dataStream.Attach(tempFile.ReadAndDelete);
        }

        static TemporaryFileStream CopyStreamToFile(Guid id, long length, BinaryReader reader)
        {
            var path = Path.Combine(Path.GetTempPath(), id.ToString());
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[1024*128];
                while (length > 0)
                {
                    var read = reader.Read(buffer, 0, (int) Math.Min(buffer.Length, length));
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
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var buffer = new BufferedStream(zip))
            using (var bson = new BsonWriter(buffer) {CloseOutput = false})
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

                var buffer = new BufferedStream(stream, 8192);
                dataStream.Write(buffer);
                buffer.Flush();
            }
        }

        class TemporaryFileStream
        {
            readonly string path;
            bool deleted;

            public TemporaryFileStream(string path)
            {
                this.path = path;
            }

            public void ReadAndDelete(Action<Stream> callback)
            {
                if (deleted) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");
                deleted = true;
                Read(callback);
                Delete();
            }

            void Read(Action<Stream> callback)
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    callback(file);
                }
            }

            void Delete()
            {
                File.Delete(path);
            }
        }

        class MessageEnvelope
        {
            public object Message { get; set; }
        }
    }
}