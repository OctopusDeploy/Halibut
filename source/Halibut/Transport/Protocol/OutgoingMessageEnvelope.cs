using System;

namespace Halibut.Transport.Protocol
{
    public class OutgoingMessageEnvelope : MessageEnvelope
    {
        public OutgoingMessageEnvelope(string id)
        {
            Id = id;
        }
        public override string Id { get; set; }
        public object Message { get; set; }
        internal override T GetMessage<T>() => (T) Message;
    }
}