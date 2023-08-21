using Halibut.Util;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureWebSocketClient : ISecureClient
    {
        [Obsolete("Replaced by HalibutLimits.RetryCountLimit")] public const int RetryCountLimit = 5;
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;

        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly ILog log;
        readonly IConnectionManager connectionManager;
        readonly ExchangeProtocolBuilder protocolBuilder;


        public SecureWebSocketClient(ExchangeProtocolBuilder protocolBuilder, 
            ServiceEndPoint serviceEndpoint,
            X509Certificate2 clientCertificate,
            AsyncHalibutFeature asyncHalibutFeature,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            ILog log,
            IConnectionManager connectionManager)
        {
            this.protocolBuilder = protocolBuilder;
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.log = log;
            this.connectionManager = connectionManager;
            this.asyncHalibutFeature = asyncHalibutFeature;
        }

        public ServiceEndPoint ServiceEndpoint => serviceEndpoint;

        [Obsolete]
        public void ExecuteTransaction(ExchangeAction protocolHandler, CancellationToken cancellationToken)
        {
            var retryInterval = ServiceEndpoint.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ServiceEndpoint.RetryCountLimit && retryAllowed && watch.Elapsed < ServiceEndpoint.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0)
                {
                    Thread.Sleep(retryInterval);
                    log.Write(EventType.OpeningNewConnection, $"Retrying connection to {serviceEndpoint.Format()} - attempt #{i}.");
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = connectionManager.AcquireConnection(protocolBuilder, new WebSocketConnectionFactory(clientCertificate, asyncHalibutFeature, halibutTimeoutsAndLimits), serviceEndpoint, log, cancellationToken);

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;

                        protocolHandler(connection.Protocol);
                    }
                    catch
                    {
                        // TODO - ASYNC ME UP!
                        connection?.Dispose();
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    connectionManager.ReleaseConnection(serviceEndpoint, connection);
                }
                catch (AuthenticationException aex)
                {
                    lastError = aex;
                    retryAllowed = false;
                }
                catch (WebSocketException wse) when (wse.Message == "Unable to connect to the remote server")
                {
                    lastError = wse;
                    retryAllowed = false;
                }
                catch (WebSocketException wse)
                {
                    lastError = wse;
                    // When the host is not found or reset the connection an immediate retry isn't going to help
                    if ((wse.InnerException?.Message.StartsWith("The remote name could not be resolved:") ?? false) ||
                        (wse.InnerException?.IsSocketConnectionReset() ?? false) ||
                        wse.IsSocketConnectionReset())
                    {
                        retryAllowed = false;
                    }
                    else
                    {
                        log.Write(EventType.Error, $"Socket communication error while connecting to {serviceEndpoint.Format()}");
                    }
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to {serviceEndpoint.Format()}", cex);
                    lastError = cex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution 
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        connectionManager.ClearPooledConnections(serviceEndpoint, log);
                    }

                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unexpected exception executing transaction.", ex);
                    lastError = ex;
                }
            }

            HandleError(lastError, retryAllowed);
        }

        public async Task ExecuteTransactionAsync(ExchangeActionAsync protocolHandler, RequestCancellationTokens requestCancellationTokens)
        {
            var retryInterval = ServiceEndpoint.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ServiceEndpoint.RetryCountLimit && retryAllowed && watch.Elapsed < ServiceEndpoint.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(retryInterval, requestCancellationTokens.LinkedCancellationToken).ConfigureAwait(false);
                    log.Write(EventType.OpeningNewConnection, $"Retrying connection to {serviceEndpoint.Format()} - attempt #{i}.");
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = await connectionManager.AcquireConnectionAsync(
                            protocolBuilder,
                            new WebSocketConnectionFactory(clientCertificate, asyncHalibutFeature, halibutTimeoutsAndLimits), 
                            serviceEndpoint,
                            log, 
                            requestCancellationTokens.LinkedCancellationToken).ConfigureAwait(false);

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;

                        // TODO: Enhancement: Pass the RequestCancellationTokens to the protocol handler so that it can cancel 
                        // PrepareExchangeAsClientAsync as part of the ConnectingCancellationToken being cancelled
                        await protocolHandler(connection.Protocol, requestCancellationTokens.InProgressRequestCancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        connection?.Dispose();
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    await connectionManager.ReleaseConnectionAsync(serviceEndpoint, connection, requestCancellationTokens.InProgressRequestCancellationToken);
                }
                catch (AuthenticationException aex)
                {
                    lastError = aex;
                    retryAllowed = false;
                }
                catch (WebSocketException wse) when (wse.Message == "Unable to connect to the remote server")
                {
                    lastError = wse;
                    retryAllowed = false;
                }
                catch (WebSocketException wse)
                {
                    lastError = wse;
                    // When the host is not found or reset the connection an immediate retry isn't going to help
                    if ((wse.InnerException?.Message.StartsWith("The remote name could not be resolved:") ?? false) ||
                        (wse.InnerException?.IsSocketConnectionReset() ?? false) ||
                        wse.IsSocketConnectionReset())
                    {
                        retryAllowed = false;
                    }
                    else
                    {
                        log.Write(EventType.Error, $"Socket communication error while connecting to {serviceEndpoint.Format()}");
                    }
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to {serviceEndpoint.Format()}", cex);
                    lastError = cex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution 
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        await connectionManager.ClearPooledConnectionsAsync(serviceEndpoint, log, requestCancellationTokens.InProgressRequestCancellationToken);
                    }

                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unexpected exception executing transaction.", ex);
                    lastError = ex;
                }
            }

            HandleError(lastError, retryAllowed);
        }

        void HandleError(Exception lastError, bool retryAllowed)
        {
            if (lastError == null)
                return;

            lastError = lastError.UnpackFromContainers();

            var innermost = lastError;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            
            if (innermost is SocketException se && !retryAllowed)
                if (se.SocketErrorCode == SocketError.ConnectionAborted || se.SocketErrorCode == SocketError.ConnectionReset)
                    throw new HalibutClientException($"The server {ServiceEndpoint.BaseUri} aborted the connection before it was fully established. This usually means that the server rejected the certificate that we provided. We provided a certificate with a thumbprint of '{clientCertificate.Thumbprint}'.");

            
            var error = new StringBuilder();
            error.Append("An error occurred when sending a request to '").Append(serviceEndpoint.BaseUri).Append("', ");
            error.Append(retryAllowed ? "before the request could begin: " : "after the request began: ");
            error.Append(lastError.Message);

            throw new HalibutClientException(error.ToString(), lastError);
        }
    }
}