using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Transport.Protocol
{
    public delegate MessageExchangeProtocol ExchangeProtocolBuilder(Stream stream, ILog log);

    public delegate void ExchangeAction(MessageExchangeProtocol protocol);

    public delegate Task ExchangeActionAsync(MessageExchangeProtocol protocol);

    /// <summary>
    /// Implements the core message exchange protocol for both the client and server.
    /// </summary>
    public class MessageExchangeProtocol
    {
        public readonly Version VersionTwo = new Version(2, 0);

        readonly IMessageExchangeStream stream;
        readonly ILog log;
        bool identified;
        Version remoteVersion = new Version(1, 0);
        volatile bool acceptClientRequests = true;

        public MessageExchangeProtocol(IMessageExchangeStream stream, ILog log)
        {
            this.stream = stream;
            this.log = log;
        }

        public IResponseMessage ExchangeAsClient(IRequestMessage request, Func<IRequestMessage, Version, Version, IRequestMessage> mapper)
        {
            PrepareExchangeAsClient();

            var mappedRequest = mapper(request, stream.LocalVersion, remoteVersion);

            stream.Send(mappedRequest);

            IResponseMessage response;

            if (remoteVersion == VersionTwo && stream.LocalVersion == VersionTwo)
            {
                response = stream.Receive<ResponseMessageV2>();
            }
            else
            {
                response = stream.Receive<ResponseMessage>();
            }

            return response;
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
                    var identity = stream.IdentifyAsClient();
                    remoteVersion = identity.Version;
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

        public void ExchangeAsSubscriber(Uri subscriptionId, Func<IRequestMessage, IResponseMessage> incomingRequestProcessor, int maxAttempts = int.MaxValue)
        {
            if (!identified)
            {
                var identity = stream.IdentifyAsSubscriber(subscriptionId.ToString());
                remoteVersion = identity.Version;
                identified = true;
            }

            for (var i = 0; i < maxAttempts; i++)
            {
                ReceiveAndProcessRequest(stream, incomingRequestProcessor);
            }
        }

        void ReceiveAndProcessRequest(IMessageExchangeStream stream, Func<IRequestMessage, IResponseMessage> incomingRequestProcessor)
        {
            IRequestMessage request;

            if (remoteVersion == VersionTwo && stream.LocalVersion == VersionTwo)
            {
                request = stream.Receive<RequestMessageV2>();
            }
            else
            {
                request = stream.Receive<RequestMessage>();
            }

            if (request != null)
            {
                var response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);
                stream.Send(response);
            }

            stream.SendNext();
            stream.ExpectProceeed();
        }

        public void ExchangeAsServer(Func<IRequestMessage, IResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
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

        public async Task ExchangeAsServerAsync(Func<IRequestMessage, IResponseMessage> incomingRequestProcessor, Func<RemoteIdentity, IPendingRequestQueue> pendingRequests)
        {
            var identity = stream.ReadRemoteIdentity();
            remoteVersion = identity.Version;
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

        void ProcessClientRequests(Func<IRequestMessage, IResponseMessage> incomingRequestProcessor)
        {
            while (acceptClientRequests)
            {
                IRequestMessage request = null;
                IResponseMessage response;

                try
                {
                    if (remoteVersion == VersionTwo && stream.LocalVersion == VersionTwo)
                    {
                        request = stream.Receive<RequestMessageV2>();
                    }
                    else
                    {
                        request = stream.Receive<RequestMessage>();
                    }

                    if (request == null || !acceptClientRequests)
                        return;

                    response = InvokeAndWrapAnyExceptions(request, incomingRequestProcessor);

                    if (!acceptClientRequests)
                        return;
                }
                catch (Exception ex)
                {
                    response = ResponseMessageFactory.FromException(request, ex);
                }

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

        bool ProcessReceiverInternal(IPendingRequestQueue pendingRequests, IRequestMessage nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    IResponseMessage response;

                    if (remoteVersion == VersionTwo && stream.LocalVersion == VersionTwo)
                    {
                        response = stream.Receive<ResponseMessageV2>();
                    }
                    else
                    {
                        response = stream.Receive<ResponseMessage>();
                    }

                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessageFactory.FromException(nextRequest, ex);
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

        async Task<bool> ProcessReceiverInternalAsync(IPendingRequestQueue pendingRequests, IRequestMessage nextRequest)
        {
            try
            {
                stream.Send(nextRequest);
                if (nextRequest != null)
                {
                    IResponseMessage response;

                    if (remoteVersion == VersionTwo && stream.LocalVersion == VersionTwo)
                    {
                        response = stream.Receive<ResponseMessageV2>();
                    }
                    else
                    {
                        response = stream.Receive<ResponseMessage>();
                    }

                    pendingRequests.ApplyResponse(response, nextRequest.Destination);
                }
            }
            catch (Exception ex)
            {
                if (nextRequest != null)
                {
                    var response = ResponseMessageFactory.FromException(nextRequest, ex);
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

        static IResponseMessage InvokeAndWrapAnyExceptions(IRequestMessage request, Func<IRequestMessage, IResponseMessage> incomingRequestProcessor)
        {
            try
            {
                return incomingRequestProcessor(request);
            }
            catch (Exception ex)
            {
                return ResponseMessageFactory.FromException(request, ex);
            }
        }
    }
}