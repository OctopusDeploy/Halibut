using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Transport.Protocol
{
    public delegate MessageExchangeProtocol ExchangeProtocolBuilder(Stream stream, ILog log);

    [Obsolete]
    public delegate void ExchangeAction(MessageExchangeProtocol protocol);
    public delegate Task ExchangeActionAsync(MessageExchangeProtocol protocol, CancellationToken cancellationToken);

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

        [Obsolete]
        public ResponseMessage ExchangeAsClient(RequestMessage request)
        {
            PrepareExchangeAsClient();

            stream.Send(request);
            return stream.Receive<ResponseMessage>();
        }

        public async Task<ResponseMessage> ExchangeAsClientAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            await PrepareExchangeAsClientAsync(cancellationToken);

            await stream.SendAsync(request, cancellationToken);
            return await stream.ReceiveAsync<ResponseMessage>(cancellationToken);
        }

        public void StopAcceptingClientRequests()
        {
            acceptClientRequests = false;
        }

        [Obsolete]
        public void EndCommunicationWithServer()
        {
            stream.SendEnd();
        }

        public async Task EndCommunicationWithServerAsync(CancellationToken cancellationToken)
        {
            await stream.SendEndAsync(cancellationToken);
        }

        [Obsolete]
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

        async Task PrepareExchangeAsClientAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!identified)
                {
                    await stream.IdentifyAsClientAsync(cancellationToken);
                    identified = true;
                }
                else
                {
                    await stream.SendNextAsync(cancellationToken);
                    await stream.ExpectProceedAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new ConnectionInitializationFailedException(ex);
            }
        }

        [Obsolete]
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

        public async Task ExchangeAsSubscriberAsync(Uri subscriptionId, Func<RequestMessage, ResponseMessage> incomingRequestProcessor, int maxAttempts, CancellationToken cancellationToken)
        {
            if (!identified)
            {
                await stream.IdentifyAsSubscriberAsync(subscriptionId.ToString(), cancellationToken);
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                await ReceiveAndProcessRequestAsync(stream, incomingRequestProcessor, cancellationToken);
            }
        }

        [Obsolete]
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

        static async Task ReceiveAndProcessRequestAsync(IMessageExchangeStream stream, Func<RequestMessage, ResponseMessage> incomingRequestProcessor, CancellationToken cancellationToken)
        {
            var request = await stream.ReceiveAsync<RequestMessage>(cancellationToken);

            if (request != null)
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                await stream.SendAsync(response, cancellationToken);
            }

            await stream.SendNextAsync(cancellationToken);
            await stream.ExpectProceedAsync(cancellationToken);
        }

        [Obsolete]
        public async Task ExchangeAsServerSynchronouslyAsync(Func<RequestMessage, ResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            stream.IdentifyAsServer();
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    ProcessClientRequests(incomingRequestProcessor);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriberSynchronouslyAsync(pendingRequests(identity));
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        public async Task ExchangeAsServerAsync(Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests, CancellationToken cancellationToken)
        {
            var identity = await stream.ReadRemoteIdentityAsync(cancellationToken);
            await stream.IdentifyAsServerAsync(cancellationToken);

            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    await ProcessClientRequestsAsync(incomingRequestProcessor, cancellationToken);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriberAsync(pendingRequests(identity), cancellationToken);
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        [Obsolete]
        void ProcessClientRequests(Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            while (acceptClientRequests)
            {
                var request = stream.Receive<RequestMessage>();
                if (request == null || !acceptClientRequests)
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
        
        async Task ProcessClientRequestsAsync(Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, CancellationToken cancellationToken)
        {
            while (acceptClientRequests && !cancellationToken.IsCancellationRequested)
            {
                var request = await stream.ReceiveAsync<RequestMessage>(cancellationToken);

                if (request == null || !acceptClientRequests)
                {
                    return;
                }

                var response = await InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);

                if (!acceptClientRequests || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                
                await stream.SendAsync(response, cancellationToken);

                try
                {
                    if (!acceptClientRequests || cancellationToken.IsCancellationRequested || !await stream.ExpectNextOrEndAsync(cancellationToken))
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex.IsSocketConnectionTimeout())
                {
                    // We get socket timeout on the Listening side (a Listening Tentacle in Octopus use) as part of normal operation
                    // if we don't hear from the other end within our TcpRx Timeout.
                    log.Write(EventType.Diagnostic, "No messages received from client for timeout period. Connection closed and will be re-opened when required");
                    
                    return;
                }

                await stream.SendProceedAsync(cancellationToken);
            }
        }

        [Obsolete]
        async Task ProcessSubscriberSynchronouslyAsync(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = await pendingRequests.DequeueAsync(CancellationToken.None);

                var success = await ProcessReceiverInternalSynchronouslyAsync(pendingRequests, nextRequest);
                if (!success)
                    return;
            }
        }

        async Task ProcessSubscriberAsync(IPendingRequestQueue pendingRequests, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextRequest = await pendingRequests.DequeueAsync(cancellationToken);

                var success = await ProcessReceiverInternalAsync(pendingRequests, nextRequest, cancellationToken);
                
                if (!success)
                {
                    return;
                }
            }
        }

        [Obsolete]
        async Task<bool> ProcessReceiverInternalSynchronouslyAsync(IPendingRequestQueue pendingRequests, RequestMessage nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    var response = stream.Receive<ResponseMessage>();
                    await pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessage.FromException(nextRequest, ex);
                    await pendingRequests.ApplyResponse(response, nextRequest.Destination);
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
        
        async Task<bool> ProcessReceiverInternalAsync(IPendingRequestQueue pendingRequests, RequestMessage nextRequest, CancellationToken cancellationToken)
        {
            try
            {
                await stream.SendAsync(nextRequest, cancellationToken);
                if (nextRequest != null)
                {
                    var response = await stream.ReceiveAsync<ResponseMessage>(cancellationToken);
                    await pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessage.FromException(nextRequest, ex);
                    await pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }

                return false;
            }

            try
            {
                if (!await stream.ExpectNextOrEndAsync(cancellationToken))
                {
                    return false;
                }
            }
            catch (Exception ex) when (ex.IsSocketConnectionTimeout())
            {
                // We get socket timeout on the server when the network connection to a polling client drops
                // (in Octopus this is the server for a Polling Tentacle)
                // In normal operation a client will poll more often than the timeout so we shouldn't see this.
                log.Write(EventType.Diagnostic, "No messages received from client for timeout period. This may be due to network problems. Connection will be re-opened when required.");

                return false;
            }

            await stream.SendProceedAsync(cancellationToken);
            return true;
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
        
        static async Task<ResponseMessage> InvokeAndWrapAnyExceptions(RequestMessage request, Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor)
        {
            try
            {
                return await incomingRequestProcessor(request);
            }
            catch (Exception ex)
            {
                return ResponseMessage.FromException(request, ex);
            }
        }
    }
}
