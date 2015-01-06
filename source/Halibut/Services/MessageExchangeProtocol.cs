using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            serializer = DefaultJsonSerializer.Factory();
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
            var inboundRequests = Read<List<RequestMessage>>(stream);
            var inboundResponses = new List<ResponseMessage>();

            foreach (var inbound in inboundRequests)
            {
                var response = serviceInvoker(inbound);
                inboundResponses.Add(response);
            }

            Write(inboundResponses, stream);

            return inboundRequests.Count;
        }

        int SendOutgoingRequests(Stream stream)
        {
            var requests = queue.DequeueRequests();
            Write(requests, stream);

            var responses = Read<List<ResponseMessage>>(stream);
            queue.ApplyResponses(responses);
            return requests.Count;
        }

        static JsonSerializer CreateDefault()
        {
            var settings = new JsonSerializerSettings { Formatting = Formatting.None };
            var serializer = JsonSerializer.Create(settings);
            serializer.TypeNameHandling = TypeNameHandling.All;
            serializer.TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple;
            return serializer;
        }

        T Read<T>(Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Decompress, true))
            using (var bson = new BsonReader(zip) { CloseInput = false })
            {
                return serializer.Deserialize<T>(bson);
            }
        }

        void Write<T>(T messages, Stream stream)
        {
            using (var zip = new DeflateStream(stream, CompressionMode.Compress, true))
            using (var bson = new BsonWriter(zip) { CloseOutput = false })
            {
                serializer.Serialize(bson, messages);
                bson.Flush();
            }
        }
    }
}