using System;
using System.IO;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Transport.Protocol
{
    /// <summary>
    /// Implements the core message exchange protocol for both the client and server. 
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly IMessageExchangeStream stream;
        bool identified;

        public MessageExchangeProtocol(Stream stream, ILog log)
        {
            this.stream = new MessageExchangeStream(stream, log);
        }

        public MessageExchangeProtocol(IMessageExchangeStream stream)
        {
            this.stream = stream;
        }

        public ResponseMessage ExchangeAsClient(RequestMessage request)
        {
            PrepareExchangeAsClient();

            stream.Send(request);
            return stream.Receive<ResponseMessage>();
        }

        void PrepareExchangeAsClient()
        {
            try
            {
                if (!identified)
                {
                    stream.IdentifyAsClient();
                    identified = true;
                }
                else
                {
                    stream.SendNext();
                    stream.ExpectProceeed();
                }
            }
            catch (Exception ex)
            {
                throw new ConnectionInitializationFailedException(ex);
            }
        }

        public void ExchangeAsSubscriber(Uri subscriptionId, Func<RequestMessage, ResponseMessage> incomingRequestProcessor, int maxAttempts = int.MaxValue)
        {
            if (!identified)
            {
                stream.IdentifyAsSubscriber(subscriptionId.ToString());
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                ReceiveAndProcessRequest(stream, incomingRequestProcessor);                
            }
        }

        static void ReceiveAndProcessRequest(IMessageExchangeStream stream, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            var request = stream.Receive<RequestMessage>();
            if (request != null)
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                stream.Send(response);
            }

            stream.SendNext();
            stream.ExpectProceeed();
        }

        public void ExchangeAsServer(Func<RequestMessage, ResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequests(incomingRequestProcessor);
                    break;
                case RemoteIdentityType.Subscriber:
                    ProcessSubscriber(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        void ProcessClientRequests(Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            while (true)
            {
                var request = stream.Receive<RequestMessage>();
                if (request == null)
                    continue;
                
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                stream.Send(response);

                if (!stream.ExpectNextOrEnd())
                    break;
                stream.SendProceed();
            }
        }

        void ProcessSubscriber(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = pendingRequests.Dequeue();

                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    var response = stream.Receive<ResponseMessage>();
                    pendingRequests.ApplyResponse(response);
                }

                if (!stream.ExpectNextOrEnd())
                    break;
                stream.SendProceed();
            }
        }

        static ResponseMessage InvokeAndWrapAnyExceptions(RequestMessage request, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            try
            {
                return incomingRequestProcessor(request);
            }
            catch (Exception ex)
            {
                return ResponseMessage.FromException(request, ex);
            }
        }
    }
}