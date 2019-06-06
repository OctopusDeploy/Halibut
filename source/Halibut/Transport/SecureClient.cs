using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;

namespace Halibut.Transport
{
    public class SecureClient : ISecureClient
    {
        [Obsolete("Replaced by HalibutLimits.RetryCountLimit")] public const int RetryCountLimit = 5;
        readonly ILog log;
        readonly ConnectionManager connectionManager;
        readonly X509Certificate2 clientCertificate;

        public SecureClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionManager connectionManager)
        {
            this.ServiceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.connectionManager = connectionManager;
        }

        public ServiceEndPoint ServiceEndpoint { get; }

        public void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler)
        {
            var retryInterval = ServiceEndpoint.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < ServiceEndpoint.RetryCountLimit && retryAllowed && watch.Elapsed < ServiceEndpoint.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0) log.Write(EventType.Error, "Retry attempt {0}", i);

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = connectionManager.AcquireConnection(new TcpConnectionFactory(clientCertificate), ServiceEndpoint, log);

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
                catch (AuthenticationException aex)
                {
                    log.WriteException(EventType.Error, $"Authentication failed while setting up connection to {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())}", aex);
                    lastError = aex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException cex) when (cex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    log.Write(EventType.Error, $"The remote host at {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())} refused the connection, this may mean that the expected listening service is not running.");
                    lastError = cex;
                    retryAllowed = false;
                    Thread.Sleep(retryInterval);
                }
                catch (SocketException sex)
                {
                    log.WriteException(EventType.Error, $"Socket communication error with connection to  {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())}", sex);
                    lastError = sex;
                    // When the host is not found an immediate retry isn't going to help
                    if (sex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        break;
                    }
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to  {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())}", cex);
                    lastError = cex;
                    retryAllowed = true;

                    // If this is the second failure, clear the pooled connections as a precaution
                    // against all connections in the pool being bad
                    if (i == 1)
                    {
                        connectionManager.ClearPooledConnections(ServiceEndpoint, log);
                    }

                    Thread.Sleep(retryInterval);
                }
                catch (IOException iox) when (iox.IsSocketConnectionReset())
                {
                    log.Write(EventType.Error, $"The remote host at {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())} reset the connection, this may mean that the expected listening service does not trust the thumbprint {clientCertificate.Thumbprint} or was shut down.");
                    lastError = iox;
                    Thread.Sleep(retryInterval);
                }
                catch (IOException iox) when (iox.IsSocketConnectionTimeout())
                {
                    // Received on a polling client when the network connection is lost.
                    log.Write(EventType.Error, $"The connection to the host at {(ServiceEndpoint == null ? "(Null EndPoint)" : ServiceEndpoint.BaseUri.ToString())} timed out, there may be problems with the network, connection will be retried.");
                    lastError = iox;
                    Thread.Sleep(retryInterval);
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Unexpected exception executing transaction.", ex);
                    lastError = ex;
                    Thread.Sleep(retryInterval);
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
