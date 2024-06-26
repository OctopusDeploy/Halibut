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
using Halibut.Exceptions;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Transport
{
    class SecureListeningClient : ISecureClient
    {
        readonly ILog log;
        readonly IConnectionManager connectionManager;
        readonly X509Certificate2 clientCertificate;
        readonly ExchangeProtocolBuilder exchangeProtocolBuilder;
        readonly TcpConnectionFactory tcpConnectionFactory;

        public SecureListeningClient(ExchangeProtocolBuilder exchangeProtocolBuilder,
            ServiceEndPoint serviceEndpoint,
            X509Certificate2 clientCertificate,
            ILog log,
            IConnectionManager connectionManager,
            TcpConnectionFactory tcpConnectionFactory)
        {
            this.exchangeProtocolBuilder = exchangeProtocolBuilder;
            this.ServiceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.connectionManager = connectionManager;
            this.tcpConnectionFactory = tcpConnectionFactory;
        }

        public ServiceEndPoint ServiceEndpoint { get; }
        
        public async Task ExecuteTransactionAsync(ExchangeActionAsync protocolHandler, CancellationToken cancellationToken)
        {
            var retryInterval = ServiceEndpoint.RetryListeningSleepInterval;

            Exception? lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            // It is important to know if an exception happens while connecting or while transmitting a request, 
            // as it determines what we do in Tentacle (for example, if we are cancelling).
            var hasConnected = false;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ServiceEndpoint.RetryCountLimit && retryAllowed && watch.Elapsed < ServiceEndpoint.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0)
                {
                    try
                    {
                        await Task.Delay(retryInterval, cancellationToken).ConfigureAwait(false);
                        log.Write(EventType.OpeningNewConnection, $"Retrying connection to {ServiceEndpoint.Format()} - attempt #{i}.");
                    }
                    catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                    {
                        throw new ConnectingRequestCancelledException(ex);
                    }
                }

                try
                {
                    lastError = null;

                    IConnection? connection = null;
                    try
                    {
                        try
                        {
                            connection = await connectionManager.AcquireConnectionAsync(
                                    exchangeProtocolBuilder,
                                    tcpConnectionFactory,
                                    ServiceEndpoint,
                                    log,
                                cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                        {
                            throw new ConnectingRequestCancelledException(ex);
                        }

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;
                        hasConnected = true;

                        try
                        {
                            await protocolHandler(connection.Protocol, cancellationToken).ConfigureAwait(false);
                        }
                        catch (ConnectionInitializationFailedException)
                        {
                            // ConnectionInitializationFailedException is thrown while performing pre-exchange. I.e., we have not actually started sending the request.
                            // Therefore, this is considered part of 'connecting'. 
                            hasConnected = false;
                            throw;
                        }
                        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                        {
                            throw new TransferringRequestCancelledException(ex);
                        }
                    }
                    catch
                    {
                        if (connection is not null)
                        {
                            await connection.DisposeAsync();
                        }

                        if (connectionManager.IsDisposed)
                            return;
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    await connectionManager.ReleaseConnectionAsync(ServiceEndpoint, connection, cancellationToken);
                }
                catch (AuthenticationException ex)
                {
                    log.WriteException(EventType.Error, $"Authentication failed while setting up connection to {ServiceEndpoint.Format()}", ex);
                    lastError = ex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    log.Write(EventType.Error, $"The remote host at {ServiceEndpoint.Format()} refused the connection. This may mean that the expected listening service is not running.");
                    lastError = ex;
                }
                catch (HalibutClientException ex)
                {
                    lastError = ex;
                    log.Write(EventType.Error, $"{ex.Message?.TrimEnd('.')}. Retrying in {retryInterval.TotalSeconds:n1} seconds.");
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
                        await connectionManager.ClearPooledConnectionsAsync(ServiceEndpoint, log, cancellationToken);
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
                catch (OperationCanceledException ex)
                {
                    log.WriteException(EventType.Diagnostic, "The operation was canceled", ex);
                    lastError = ex;
                    retryAllowed = false;
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unexpected exception executing transaction.", ex);
                    lastError = ex;
                }
            }

            HandleError(lastError, retryAllowed, hasConnected);
        }

        void HandleError(Exception? lastError, bool retryAllowed, bool hasConnected)
        {
            if (lastError == null)
            {
                return;
            }

            lastError = lastError.UnpackFromContainers();

            var error = new StringBuilder();
            error.Append("An error occurred when sending a request to '").Append(ServiceEndpoint.BaseUri).Append("', ");
            error.Append(retryAllowed ? "before the request could begin: " : "after the request began: ");
            error.Append(lastError.Message);
            
            
            switch (lastError)
            {
                case SocketException inner:
                    if ((inner.SocketErrorCode == SocketError.ConnectionAborted || inner.SocketErrorCode == SocketError.ConnectionReset) && retryAllowed)
                    {
                        error.Append("The server aborted the connection before it was fully established. This usually means that the server rejected the certificate that we provided. We provided a certificate with a thumbprint of '");
                        error.Append(clientCertificate.Thumbprint + "'.");
                    }
                    break;

                // We want to handle cancellation exceptions explicitly from the tentacle client. 
                case ConnectingRequestCancelledException:
                    throw new ConnectingRequestCancelledException(error.ToString(), lastError);
                case TransferringRequestCancelledException:
                    throw new TransferringRequestCancelledException(error.ToString(), lastError);
                case OperationCanceledException:
                    throw new OperationCanceledException(error.ToString(), lastError);
            }

            throw new HalibutClientException(
                error.ToString(), 
                lastError, 
                !hasConnected ? ConnectionState.Connecting : ConnectionState.Unknown);
        }
    }
}
