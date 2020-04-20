using System;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public abstract class MessageEnvelope
    {
        public static OutgoingMessageEnvelope EmptyOutgoingMessage = new OutgoingMessageEnvelope("EMPTY")
        {
            Message = null
        };
        
        public abstract string Id { get; set; }
        public ServiceEndPoint Destination { get; set; }
        internal abstract T GetMessage<T>();
        public bool IsEmpty()
        {
            return Id == "EMPTY";
        }
        
        public static MessageEnvelope FromResult(MessageEnvelope messageEnvelope, object result)
        {
            return new OutgoingMessageEnvelope(messageEnvelope.Id)
            {
                Message = new ResponseMessage {Id = messageEnvelope.Id, Result = result}
            };
        }

        public static MessageEnvelope FromError(MessageEnvelope messageEnvelope, string message)
        {
            return new OutgoingMessageEnvelope(messageEnvelope.Id)
            {
                Message = new ResponseMessage {Id = messageEnvelope.Id, Error = new ServerError {Message = message}}
            };
        }
        
        public static MessageEnvelope FromException(MessageEnvelope message, Exception ex)
        {
            return new OutgoingMessageEnvelope(message.Id)
            {
                Message = new ResponseMessage {Id = message.Id, Error = new ServerError {Message = ex.UnpackFromContainers().Message, Details = ex.ToString()}}
            };
        }
    }
}