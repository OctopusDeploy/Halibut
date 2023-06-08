using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Transport.Protocol
{
    public delegate MessageExchangeProtocol ExchangeProtocolBuilder(Stream stream, ILog log);

    public delegate void ExchangeAction(MessageExchangeProtocol protocol);

    public delegate Task ExchangeActionAsync(MessageExchangeProtocol protocol);

    public delegate ResponseMessage IncomingRequestProcessorAction(RequestMessage requestMessage);

    public delegate ResponseMessage ExceptionHandlingAction(RequestMessage requestMessage, Exception exception);

    /// <summary>
    /// Implements the core message exchange protocol for both the client and server.
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly IMessageExchangeStream stream;
        readonly ILog log;
        bool identified;
        volatile bool acceptClientRequests = true;

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

        public void StopAcceptingClientRequests()
        {
            acceptClientRequests = false;
        }

        public void EndCommunicationWithServer()
        {
            stream.SendEnd();
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

        public void ExchangeAsSubscriber(Uri subscriptionId, IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler, int maxAttempts = int.MaxValue)
        {
            if (!identified)
            {
                stream.IdentifyAsSubscriber(subscriptionId.ToString());
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                ReceiveAndProcessRequest(stream, incomingRequestProcessor, exceptionHandler);
            }
        }

        static void ReceiveAndProcessRequest(IMessageExchangeStream stream, IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler)
        {
            var request = stream.Receive<RequestMessage>();
            if (request != null)
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor, exceptionHandler);
                stream.Send(response);
            }

            stream.SendNext();
            stream.ExpectProceeed();
        }

        public void ExchangeAsServer(IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequests(incomingRequestProcessor, exceptionHandler);
                    break;
                case RemoteIdentityType.Subscriber:
                    ProcessSubscriber(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        public async Task ExchangeAsServerAsync(IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequests(incomingRequestProcessor, exceptionHandler);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriberAsync(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        void ProcessClientRequests(IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler)
        {
            while (acceptClientRequests)
            {
                var request = stream.Receive<RequestMessage>();
                if (request == null || !acceptClientRequests)
                    return;

                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor, exceptionHandler);

                if (!acceptClientRequests)
                    return;

                stream.Send(response);

                try
                {
                    if (!acceptClientRequests || !stream.ExpectNextOrEnd())
                        return;
                }
                catch (Exception ex) when (ex.IsSocketConnectionTimeout())
                {
                    // We get socket timeout on the Listening side (a Listening Tentacle in Octopus use) as part of normal operation
                    // if we don't hear from the other end within our TcpRx Timeout.
                    log.Write(EventType.Diagnostic, "No messages received from client for timeout period. Connection closed and will be re-opened when required");
                    return;
                }
                stream.SendProceed();
            }
        }

        void ProcessSubscriber(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = pendingRequests.Dequeue();

                var success = ProcessReceiverInternal(pendingRequests, nextRequest);
                if (!success)
                    return;
            }
        }

        async Task ProcessSubscriberAsync(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = await pendingRequests.DequeueAsync();

                var success = await ProcessReceiverInternalAsync(pendingRequests, nextRequest);
                if (!success)
                    return;
            }
        }

        bool ProcessReceiverInternal(IPendingRequestQueue pendingRequests, RequestMessage nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    var response = stream.Receive<ResponseMessage>();
                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessage.FromException(nextRequest, ex);
                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
                return false;
            }

            try
            {
                if (!stream.ExpectNextOrEnd())
                    return false;
            }
            catch (Exception ex) when (ex.IsSocketConnectionTimeout())
            {
                // We get socket timeout on the server when the network connection to a polling client drops
                // (in Octopus this is the server for a Polling Tentacle)
                // In normal operation a client will poll more often than the timeout so we shouldn't see this.
                log.Write(EventType.Diagnostic, "No messages received from client for timeout period. This may be due to network problems. Connection will be re-opened when required.");
                return false;
            }
            stream.SendProceed();
            return true;
        }

        async Task<bool> ProcessReceiverInternalAsync(IPendingRequestQueue pendingRequests, RequestMessage nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    var response = stream.Receive<ResponseMessage>();
                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessage.FromException(nextRequest, ex);
                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
                return false;
            }

            try
            {
                if (!await stream.ExpectNextOrEndAsync())
                    return false;
            }
            catch (Exception ex) when (ex.IsSocketConnectionTimeout())
            {
                // We get socket timeout on the server when the network connection to a polling client drops
                // (in Octopus this is the server for a Polling Tentacle)
                // In normal operation a client will poll more often than the timeout so we shouldn't see this.
                log.Write(EventType.Diagnostic, "No messages received from client for timeout period. This may be due to network problems. Connection will be re-opened when required.");
                return false;
            }
            await stream.SendProceedAsync();
            return true;
        }

        static ResponseMessage InvokeAndWrapAnyExceptions(
            RequestMessage request, IncomingRequestProcessorAction incomingRequestProcessor, ExceptionHandlingAction exceptionHandler)
        {
            try
            {
                return incomingRequestProcessor(request);
            }
            catch (Exception ex)
            {
                return exceptionHandler(request, ex);
            }
        }
    }
}