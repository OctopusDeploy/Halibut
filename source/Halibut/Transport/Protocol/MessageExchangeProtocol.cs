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

        public async Task<ResponseMessage> ExchangeAsClient(RequestMessage request)
        {
            await PrepareExchangeAsClient().ConfigureAwait(false);

            await stream.Send(request).ConfigureAwait(false);
            return await stream.Receive<ResponseMessage>().ConfigureAwait(false);
        }

        public void StopAcceptingClientRequests()
        {
            acceptClientRequests = false;
        }

        async Task PrepareExchangeAsClient()
        {
            try
            {
                if (!identified)
                {
                    await stream.IdentifyAsClient().ConfigureAwait(false);
                    identified = true;
                }
                else
                {
                    await stream.SendNext().ConfigureAwait(false);
                    await stream.ExpectProceeed().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new ConnectionInitializationFailedException(ex);
            }
        }

        public async Task ExchangeAsSubscriber(Uri subscriptionId, Func<RequestMessage, ResponseMessage> incomingRequestProcessor, int maxAttempts = int.MaxValue)
        {
            if (!identified)
            {
                await stream.IdentifyAsSubscriber(subscriptionId.ToString()).ConfigureAwait(false);
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                await ReceiveAndProcessRequest(stream, incomingRequestProcessor).ConfigureAwait(false);
            }
        }

        static async Task ReceiveAndProcessRequest(IMessageExchangeStream stream, Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            var request = await stream.Receive<RequestMessage>().ConfigureAwait(false);
            if (request != null)
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                await stream.Send(response).ConfigureAwait(false);
            }

            await stream.SendNext().ConfigureAwait(false);
            await stream.ExpectProceeed().ConfigureAwait(false);
        }

        public async Task ExchangeAsServer(Func<RequestMessage, ResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            await stream.IdentifyAsServer().ConfigureAwait(false);

            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    await ProcessClientRequests(incomingRequestProcessor).ConfigureAwait(false);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriber(pendingRequests(identity)).ConfigureAwait(false);
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        public async Task ExchangeAsServerAsync(Func<RequestMessage, ResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            await stream.IdentifyAsServer().ConfigureAwait(false);
            switch (identity.IdentityType)
            {
                case RemoteIdentityType.Client:
                    await ProcessClientRequests(incomingRequestProcessor).ConfigureAwait(false);
                    break;
                case RemoteIdentityType.Subscriber:
                    await ProcessSubscriberAsync(pendingRequests(identity)).ConfigureAwait(false);
                    break;
                default:
                    throw new ProtocolException("Unexpected remote identity: " + identity.IdentityType);
            }
        }

        async Task ProcessClientRequests(Func<RequestMessage, ResponseMessage> incomingRequestProcessor)
        {
            while (acceptClientRequests)
            {
                var request = await stream.Receive<RequestMessage>().ConfigureAwait(false);
                if (request == null || !acceptClientRequests)
                    return;

                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);

                if (!acceptClientRequests)
                    return;

                await stream.Send(response).ConfigureAwait(false);

                try
                {
                    if (!acceptClientRequests || !await stream.ExpectNextOrEnd().ConfigureAwait(false))
                        return;
                }
                catch (Exception ex) when (ex.IsSocketConnectionTimeout())
                {
                    // We get socket timeout on the Listening side (a Listening Tentacle in Octopus use) as part of normal operation
                    // if we don't hear from the other end within our TcpRx Timeout.
                    log.Write(EventType.Diagnostic, "No messages received from client for timeout period. Connection closed and will be re-opened when required");
                    return;
                }
                await stream.SendProceed().ConfigureAwait(false);
            }
        }

        async Task ProcessSubscriber(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = await pendingRequests.DequeueAsync().ConfigureAwait(false);

                var success = await ProcessReceiverInternal(pendingRequests, nextRequest).ConfigureAwait(false);
                if (!success)
                    return;
            }
        }

        async Task ProcessSubscriberAsync(IPendingRequestQueue pendingRequests)
        {
            while (true)
            {
                var nextRequest = await pendingRequests.DequeueAsync().ConfigureAwait(false);

                var success = await ProcessReceiverInternal(pendingRequests, nextRequest).ConfigureAwait(false);

                if (!success)
                    return;
            }
        }

        async Task<bool> ProcessReceiverInternal(IPendingRequestQueue pendingRequests, RequestMessage nextRequest)
        {
            try
            {
                await stream.Send(nextRequest).ConfigureAwait(false);
                if (nextRequest != null)
                {
                    var response = await stream.Receive<ResponseMessage>().ConfigureAwait(false);
                    pendingRequests.ApplyResponse(response);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessage.FromException(nextRequest, ex);
                    pendingRequests.ApplyResponse(response);
                }
                return false;
            }

            try
            {
                if (!await stream.ExpectNextOrEnd().ConfigureAwait(false))
                    return false;
            }
            catch (Exception ex) when (ex.IsSocketConnectionTimeout())
            {
                // We get socket timeout on the server when the network connection to a polling client drops
                // (in Octopus this is the server for a Polling Tentacle)
                // In normal operation a client will poll more often than the timeout so we shouldn't see this.
                log.Write(EventType.Error, "No messages received from client for timeout period. This may be due to network problems. Connection will be re-opened when required.");
                return false;
            }
            await stream.SendProceed().ConfigureAwait(false);

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
    }
}