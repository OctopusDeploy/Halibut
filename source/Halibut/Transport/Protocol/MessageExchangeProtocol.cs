using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Observability;

namespace Halibut.Transport.Protocol
{
    public delegate MessageExchangeProtocol ExchangeProtocolBuilder(Stream stream, ILog log);
    public delegate Task ExchangeActionAsync(MessageExchangeProtocol protocol, CancellationToken cancellationToken);

    /// <summary>
    /// Implements the core message exchange protocol for both the client and server.
    /// </summary>
    public class MessageExchangeProtocol
    {
        readonly IMessageExchangeStream stream;
        readonly IRpcObserver rcpObserver;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly ILog log;
        bool identified;
        volatile bool acceptClientRequests = true;

        public MessageExchangeProtocol(IMessageExchangeStream stream, IRpcObserver rcpObserver, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log)
        {
            this.stream = stream;
            this.rcpObserver = rcpObserver;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.log = log;
        }

        public async Task<ResponseMessage> ExchangeAsClientAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            rcpObserver.StartCall(request);

            try
            {
                await PrepareExchangeAsClientAsync(cancellationToken);
                
                await stream.SendAsync(request, cancellationToken);
                return await stream.ReceiveResponseAsync(cancellationToken);
            }
            finally
            {
                rcpObserver.StopCall(request);
            }
        }

        public void StopAcceptingClientRequests()
        {
            acceptClientRequests = false;
        }

        public async Task EndCommunicationWithServerAsync(CancellationToken cancellationToken)
        {
            await stream.SendEndAsync(cancellationToken);
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

        public async Task ExchangeAsSubscriberAsync(Uri subscriptionId, Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, int maxAttempts, CancellationToken cancellationToken)
        {
            if (!identified)
            {
                await stream.IdentifyAsSubscriberAsync(subscriptionId.ToString(), cancellationToken);
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                await ReceiveAndProcessRequestAsSubscriberAsync(stream, incomingRequestProcessor, cancellationToken);
            }
        }

        async Task ReceiveAndProcessRequestAsSubscriberAsync(IMessageExchangeStream stream, Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, CancellationToken cancellationToken)
        {
            var request = await stream.ReceiveRequestAsync(halibutTimeoutsAndLimits.TcpClientReceiveRequestTimeoutForPolling, cancellationToken);

            if (request != null)
            {
                var response = await InvokeAndWrapAnyExceptionsAsync(request, incomingRequestProcessor);
                await stream.SendAsync(response, cancellationToken);
            }

            await stream.SendNextAsync(cancellationToken);
            await stream.ExpectProceedAsync(cancellationToken);
        }

        public async Task ExchangeAsServerAsync(Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests, CancellationToken cancellationToken)
        {
            var identity = await GetRemoteIdentityAsync(cancellationToken);
            await IdentifyAsServerAsync(identity, cancellationToken);

            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    await ProcessClientRequestsAsync(incomingRequestProcessor, cancellationToken);
                    break;
                case RemoteIdentityType.Subscriber:
                    var pendingRequestQueue = pendingRequests(identity);
                    await ProcessSubscriberAsync(pendingRequestQueue, cancellationToken);
                    break;
                default:
                    log.Write(EventType.ErrorInIdentify, $"Remote with identify {identity.SubscriptionId} identified itself with an unknown identity type {identity.IdentityType}");
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        async Task<RemoteIdentity> GetRemoteIdentityAsync(CancellationToken cancellationToken)
        {
            try
            {
                var identity = await stream.ReadRemoteIdentityAsync(cancellationToken);
                return identity;
            }
            catch (Exception e)
            {
                log.WriteException(EventType.ErrorInIdentify, "Remote failed to identify itself.", e);
                throw;
            }
        }

        async Task IdentifyAsServerAsync(RemoteIdentity identityOfRemote, CancellationToken cancellationToken)
        {
            try
            {
                await stream.IdentifyAsServerAsync(cancellationToken);
            }
            catch (Exception e)
            {
                log.WriteException(EventType.ErrorInIdentify, $"Failed to identify as server to the previously identified remote {identityOfRemote.SubscriptionId} of type {identityOfRemote.IdentityType}", e);
                throw;
            }
        }

        async Task ProcessClientRequestsAsync(Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor, CancellationToken cancellationToken)
        {
            while (acceptClientRequests && !cancellationToken.IsCancellationRequested)
            {
                // This timeout is probably too high since we know that we either just send identification control messages
                // or we just sent NEXT and PROCEED control messages.
                var request = await stream.ReceiveRequestAsync(halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout, cancellationToken);

                if (request == null || !acceptClientRequests)
                {
                    return;
                }

                var response = await InvokeAndWrapAnyExceptionsAsync(request, incomingRequestProcessor);

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
        
        async Task<bool> ProcessReceiverInternalAsync(IPendingRequestQueue pendingRequests, RequestMessage nextRequest, CancellationToken cancellationToken)
        {
            try
            {
                if (nextRequest != null)
                {
                    var response = await SendAndReceiveRequest(nextRequest, cancellationToken);
                    await pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
                else
                {
                    await stream.SendAsync(nextRequest, cancellationToken);
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
        
        async Task<ResponseMessage> SendAndReceiveRequest(RequestMessage nextRequest, CancellationToken cancellationToken)
        {
            rcpObserver.StartCall(nextRequest);

            try
            {
                await stream.SendAsync(nextRequest, cancellationToken);
                return await stream.ReceiveResponseAsync(cancellationToken);
            }
            finally
            {
                rcpObserver.StopCall(nextRequest);
            }
        }
        
        static async Task<ResponseMessage> InvokeAndWrapAnyExceptionsAsync(RequestMessage request, Func<RequestMessage, Task<ResponseMessage>> incomingRequestProcessor)
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
