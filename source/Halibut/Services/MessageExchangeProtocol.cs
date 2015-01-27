using System;
using System.IO;
using Halibut.Diagnostics;
using Halibut.Protocol;

namespace Halibut.Services
{
    /// <summary>
    /// Implements the core message exchange protocol for both the client and server. 
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly ILog log;
        readonly IMessageExchangeStream stream;
        bool identified;

        public MessageExchangeProtocol(Stream stream, ILog log)
        {
            this.stream = new MessageExchangeStream(stream, log);
            this.log = log;
        }

        public MessageExchangeProtocol(IMessageExchangeStream stream, ILog log)
        {
            this.stream = stream;
            this.log = log;
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
                    // First time connecting, so identify ourselves
                    stream.IdentifyAsClient();
                    identified = true;
                }

                stream.SendHello();
                stream.ExpectProceeed();
            }
            catch (Exception ex)
            {
                throw new ConnectionInitializationFailedException(ex);
            }
        }

        public int ExchangeAsSubscriber(Uri subscriptionId, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            // SEND: MX-SUBSCRIBER 1.0 [subid]
            // RECV: MX-SERVER 1.0
            // RECV: Request -> service invoker
            // SEND: Response
            // Repeat while request != null
            if (!identified)
            {
                // First time connecting, so identify ourselves
                stream.IdentifyAsSubscriber(subscriptionId.ToString());
                identified = true;
            }

            var requestsProcessed = 0;
            while (ReceiveAndProcessRequest(stream, incomingRequestProcessor)) requestsProcessed++;
            return requestsProcessed;
        }

        static bool ReceiveAndProcessRequest(IMessageExchangeStream stream, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            stream.SendHello();
            stream.ExpectProceeed();

            var request = stream.Receive<RequestMessage>();
            if (request == null) return false;
            var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
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
                stream.ExpectHello();
                stream.SendProceed();

                var request = stream.Receive<RequestMessage>();
                if (request == null)
                    break;

                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                stream.Send(response);
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
                return ResponseMessage.FromException(request, ex.UnpackFromContainers());
            }
        }

        void ProcessSubscriber(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                stream.ExpectHello();
                stream.SendProceed();

                // TODO: Error handling
                var nextRequest = pendingRequests.Dequeue();

                stream.Send(nextRequest);
                if (nextRequest == null) 
                    continue;

                var response = stream.Receive<ResponseMessage>();
                pendingRequests.ApplyResponse(response);
            }
        }
    }
}