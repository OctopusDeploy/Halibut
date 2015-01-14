using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using Halibut.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Services
{
    /// <summary>
    /// Implements the core message exchange protocol for both the client and server. 
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly IMessageExchangeParticipant participant;
        readonly Func<RequestMessage, ResponseMessage> serviceInvoker;
        readonly JsonSerializer serializer;
        IPendingRequestQueue queue;

        public MessageExchangeProtocol(IMessageExchangeParticipant participant, Func<RequestMessage, ResponseMessage> serviceInvoker)
        {
            serializer = Serializer();
            this.participant = participant;
            this.serviceInvoker = serviceInvoker;
        }

        public static Func<JsonSerializer> Serializer = CreateDefault;

        public void IdentifyAsClient(Uri subscriptionName, Stream stream)
        {
            SendIdentity(subscriptionName, stream);
            ReceiveIdentity(stream);
        }

        public void IdentifyAsServer(Uri subscriptionName, Stream stream)
        {
            ReceiveIdentity(stream);
            SendIdentity(subscriptionName, stream);
        }

        void SendIdentity(Uri subscriptionName, Stream stream)
        {
            Write(new IdentificationMessage(subscriptionName), stream);
        }

        void ReceiveIdentity(Stream stream)
        {
            var id = Read<IdentificationMessage>(stream);
            queue = participant.SelectQueue(id);
        }

        public int ExchangeAsClient(Stream stream)
        {
            return SendOutgoingRequests(stream)
                + ReceiveIncomingRequests(stream);
        }

        public int ExchangeAsServer(Stream stream)
        {
            return ReceiveIncomingRequests(stream)
                + SendOutgoingRequests(stream);
        }

        int ReceiveIncomingRequests(Stream stream)
        {
            var inboundRequests = Read<RequestMessage>(stream);
            var inboundResponse = inboundRequests == null ? null : serviceInvoker(inboundRequests);
            Write(inboundResponse, stream);
            return inboundRequests == null ? 0 : 1;
        }

        int SendOutgoingRequests(Stream stream)
        {
            var request = queue.Dequeue();
            Write(request, stream);
            var responses = Read<ResponseMessage>(stream);
            queue.ApplyResponse(responses);
            return request == null ? 0 : 1;
        }

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

        T Read<T>(Stream stream)
        {
            using (var capture = StreamCapture.New())
            {
                var result = ReadBsonMessage<T>(stream);
                ReadStreams(capture, stream);
                return result;
            }
        }

        T ReadBsonMessage<T>(Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var buffer = new BufferedStream(zip, 8192))
            using (var bson = new BsonReader(buffer) { CloseInput = false })
            {
                return (T)serializer.Deserialize<MessageEnvelope>(bson).Message;
            }
        }

        static void ReadStreams(StreamCapture capture, Stream stream)
        {
            var expected = capture.DeserializedStreams.Count;

            for (var i = 0; i < expected; i++)
            {
                ReadStream(capture, stream);
            }
        }

        static void ReadStream(StreamCapture capture, Stream stream)
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

        void Write<T>(T messages, Stream stream)
        {
            using (var capture = StreamCapture.New())
            {
                WriteBsonMessage(messages, stream);
                WriteEachStream(capture.SerializedStreams, stream);
            }
        }

        void WriteBsonMessage<T>(T messages, Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var buffer = new BufferedStream(zip))
            using (var bson = new BsonWriter(buffer) {CloseOutput = false})
            {
                serializer.Serialize(bson, new MessageEnvelope { Message = messages });
                bson.Flush();
            }
        }

        static void WriteEachStream(IEnumerable<DataStream> streams, Stream stream)
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