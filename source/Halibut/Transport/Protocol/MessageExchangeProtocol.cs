using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        readonly ILog log;
        bool identified;
        volatile bool acceptClientRequests = true;

        public MessageExchangeProtocol(Stream stream, ILog log)
        {
            this.stream = new MessageExchangeStream(stream, log);
            this.log = log;
        }

        public MessageExchangeProtocol(IMessageExchangeStream stream)
        {
            this.stream = stream;
        }

        public IncomingMessageEnvelope ExchangeAsClient(MessageEnvelope request)
        {
            PrepareExchangeAsClient();

            stream.Send(request);
            return stream.Receive();
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

        public void ExchangeAsSubscriber(Uri subscriptionId, Func<MessageEnvelope, MessageEnvelope> incomingRequestProcessor, int maxAttempts = int.MaxValue)
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

        static void ReceiveAndProcessRequest(IMessageExchangeStream stream, Func<MessageEnvelope, MessageEnvelope> incomingRequestProcessor)
        {
            var request = stream.Receive();
            if (!request.IsEmpty())
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                stream.Send(response);
            }

            stream.SendNext();
            stream.ExpectProceeed();
        }

        public void ExchangeAsServer(Func<MessageEnvelope, MessageEnvelope> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
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

        public async Task ExchangeAsServerAsync(Func<MessageEnvelope, MessageEnvelope> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequests(incomingRequestProcessor);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriberAsync(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        void ProcessClientRequests(Func<MessageEnvelope, MessageEnvelope> incomingRequestProcessor)
        {
            while (acceptClientRequests)
            {
                var request = stream.Receive();
                if (request.IsEmpty() || !acceptClientRequests)
                    return;

                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);

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

        bool ProcessReceiverInternal(IPendingRequestQueue pendingRequests, MessageEnvelope nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (!nextRequest.IsEmpty())
                {
                    var response = stream.Receive();
                    pendingRequests.ApplyResponse(response);
                }
            }
            catch (Exception ex)
            {
                if (!nextRequest.IsEmpty())
                {
                    var response = MessageEnvelope.FromException(nextRequest, ex);
                    pendingRequests.ApplyResponse(response);
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

        async Task<bool> ProcessReceiverInternalAsync(IPendingRequestQueue pendingRequests, MessageEnvelope nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (!nextRequest.IsEmpty())
                {
                    var response = stream.Receive();
                    pendingRequests.ApplyResponse(response);
                }
            }
            catch (Exception ex)
            {
                if (!nextRequest.IsEmpty())
                {
                    var response = MessageEnvelope.FromException(nextRequest, ex);
                    pendingRequests.ApplyResponse(response);
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

        static MessageEnvelope InvokeAndWrapAnyExceptions(IncomingMessageEnvelope request, Func<IncomingMessageEnvelope, MessageEnvelope> incomingRequestProcessor)
        {
            try
            {
                return incomingRequestProcessor(request);
            }
            catch (Exception ex)
            {
                return MessageEnvelope.FromException(request, ex);
            }
        }
    }
}