using System;
using System.IO;
using System.Linq;
using Halibut.Util;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace Halibut.Transport.Protocol
{
    public class IncomingMessageEnvelope : MessageEnvelope
    {
        string id;

        public override string Id
        {
            //backward compatibility, retrieve id from the message
            get => !string.IsNullOrWhiteSpace(id) ? id : InternalMessage.FromBson<JObject>()["Message"]["id"].ToString();
            set => id = value;
        }
        internal byte[] InternalMessage { get; set; }
        internal override T GetMessage<T>()
        {
            var envelope = InternalMessage.FromBson<OutgoingMessageEnvelope>();
            return (T) envelope.Message;
        }
    }
}