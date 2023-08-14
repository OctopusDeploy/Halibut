using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Transport
{
    public class SecureClient : ISecureClient
    {
        [Obsolete("Replaced by HalibutLimits.RetryCountLimit")] public const int RetryCountLimit = 5;
        readonly ILog log;
        readonly ConnectionManager connectionManager;
        readonly X509Certificate2 clientCertificate;
        readonly ExchangeProtocolBuilder protocolBuilder;

        public SecureClient(ExchangeProtocolBuilder protocolBuilder, ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionManager connectionManager)
        {
            this.protocolBuilder = protocolBuilder;
            this.ServiceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.connectionManager = connectionManager;
        }

        public ServiceEndPoint ServiceEndpoint { get; }
        
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
                    log.Write(EventType.OpeningNewConnection, $"Retrying connection to {ServiceEndpoint.Format()} - attempt #{i}.");
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = connectionManager.AcquireConnection(protocolBuilder, new TcpConnectionFactory(clientCertificate), ServiceEndpoint, log, cancellationToken);

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;

                        protocolHandler(connection.Protocol);
                    }
                    catch
                    {
                        connection?.Dispose();
                        if (connectionManager.IsDisposed)
                            return;
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    connectionManager.ReleaseConnection(ServiceEndpoint, connection);
                }
                catch (AuthenticationException ex)
                {
                    lastError = ex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} refused the connection. This may mean that the expected listening service is not running.");
                    lastError = ex;
                    retryAllowed = false;
                }
                catch (SocketException ex)
                {
                    log.WriteException(EventType.Error, $"Socket communication error while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    // When the host is not found an immediate retry isn't going to help
                    if (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        break;
                    }
                }
                catch (ConnectionInitializationFailedException ex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        connectionManager.ClearPooledConnections(ServiceEndpoint, log);
                    }
                }
                catch (IOException ex) when (ex.IsSocketConnectionReset())
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} reset the connection. This may mean that the expected listening service does not trust the thumbprint {clientCertificate.Thumbprint} or was shut down.");
                    lastError = ex;
                }
                catch (IOException ex) when (ex.IsSocketConnectionTimeout())
                {
                    // Received on a polling client when the network connection is lost.
                    log.Write(EventType.Error, $"The connection to the host at {ServiceEndpoint.Format()} timed out. There may be problems with the network. The connection will be retried.");
                    lastError = ex;
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
                    log.Write(EventType.OpeningNewConnection, $"Retrying connection to {ServiceEndpoint.Format()} - attempt #{i}.");
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = await connectionManager.AcquireConnectionAsync(
                            protocolBuilder, 
                            new TcpConnectionFactory(clientCertificate), 
                            ServiceEndpoint, 
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
                        // TODO - ASYNC ME UP!
                        connection?.Dispose();
                        if (connectionManager.IsDisposed)
                            return;
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    await connectionManager.ReleaseConnectionAsync(ServiceEndpoint, connection, requestCancellationTokens.InProgressRequestCancellationToken);
                }
                catch (AuthenticationException ex)
                {
                    lastError = ex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} refused the connection. This may mean that the expected listening service is not running.");
                    lastError = ex;
                    retryAllowed = false;
                }
                catch (SocketException ex)
                {
                    log.WriteException(EventType.Error, $"Socket communication error while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    // When the host is not found an immediate retry isn't going to help
                    if (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        break;
                    }
                }
                catch (ConnectionInitializationFailedException ex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        await connectionManager.ClearPooledConnectionsAsync(ServiceEndpoint, log, requestCancellationTokens.InProgressRequestCancellationToken);
                    }
                }
                catch (IOException ex) when (ex.IsSocketConnectionReset())
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} reset the connection. This may mean that the expected listening service does not trust the thumbprint {clientCertificate.Thumbprint} or was shut down.");
                    lastError = ex;
                }
                catch (IOException ex) when (ex.IsSocketConnectionTimeout())
                {
                    // Received on a polling client when the network connection is lost.
                    log.Write(EventType.Error, $"The connection to the host at {ServiceEndpoint.Format()} timed out. There may be problems with the network. The connection will be retried.");
                    lastError = ex;
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
            error.Append("An error occurred when sending a request to '").Append(ServiceEndpoint.BaseUri).Append("', ");
            error.Append(retryAllowed ? "before the request could begin: " : "after the request began: ");
            error.Append(lastError.Message);
            
            throw new HalibutClientException(error.ToString(), lastError);
        }
    }
}
