// WebSocketClient on .NET Core does not yet support the validation (or bypass) of the remote certificate. It does
// not use the ServicePointManager callback, nor has an option to specify a callback.
// This means we cannot validate the remote is presenting the correct certificate
// See https://github.com/dotnet/corefx/issues/12038

#if SUPPORTS_WEB_SOCKET_CLIENT
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureWebSocketClient : ISecureClient
    {
        [Obsolete("Replaced by HalibutLimits.RetryCountLimit")] public const int RetryCountLimit = 5;
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;
        readonly ILog log;
        readonly ConnectionManager connectionManager;

        public SecureWebSocketClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionManager connectionManager)
        {
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.connectionManager = connectionManager;
        }

        public ServiceEndPoint ServiceEndpoint => serviceEndpoint;

        public void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler)
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
                    log.Write(EventType.Error, "Retry attempt {0}", i);
                }

                try
                {
                    lastError = null;

                    IConnection connection = null;
                    try
                    {
                        connection = connectionManager.AcquireConnection(new WebSocketConnectionFactory(clientCertificate), serviceEndpoint, log);

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;

                        protocolHandler(connection.Protocol);
                    }
                    catch
                    {
                        connection?.Dispose();
                        throw;
                    }

                    // Only return the connection to the pool if all went well
                    connectionManager.ReleaseConnection(serviceEndpoint, connection);
                }
                catch (AuthenticationException aex)
                {
                    log.WriteException(EventType.Error, $"Authentication failed while setting up connection to {(serviceEndpoint == null ? "(Null EndPoint)" : serviceEndpoint.BaseUri.ToString())}", aex);
                    lastError = aex;
                    retryAllowed = false;
                    break;
                }
                catch (WebSocketException wse) when (wse.Message == "Unable to connect to the remote server")
                {
                    log.Write(EventType.Error, $"The remote host at {(serviceEndpoint == null ? "(Null EndPoint)" : serviceEndpoint.BaseUri.ToString())} refused the connection, this may mean that the expected listening service is not running, or it's SSL certificate has not been configured correctly.");
                    lastError = wse;
                }
                catch (WebSocketException wse)
                {
                    log.WriteException(EventType.Error, $"Socket communication error with connection to  {(serviceEndpoint == null ? "(Null EndPoint)" : serviceEndpoint.BaseUri.ToString())}", wse);
                    lastError = wse;
                    // When the host is not found an immediate retry isn't going to help
                    if (wse.InnerException?.Message.StartsWith("The remote name could not be resolved:") ?? false)
                        break;
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    log.WriteException(EventType.Error, $"Connection initialization failed while connecting to  {(serviceEndpoint == null ? "(Null EndPoint)" : serviceEndpoint.BaseUri.ToString())}", cex);
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

        void HandleError(Exception lastError, bool retryAllowed)
        {
            if (lastError == null)
                return;

            lastError = lastError.UnpackFromContainers();

            var error = new StringBuilder();
            error.Append("An error occurred when sending a request to '").Append(serviceEndpoint.BaseUri).Append("', ");
            error.Append(retryAllowed ? "before the request could begin: " : "after the request began: ");
            error.Append(lastError.Message);

            var inner = lastError as SocketException;
            if (inner != null)
            {
                if ((inner.SocketErrorCode == SocketError.ConnectionAborted || inner.SocketErrorCode == SocketError.ConnectionReset) && retryAllowed)
                {
                    error.Append("The server aborted the connection before it was fully established. This usually means that the server rejected the certificate that we provided. We provided a certificate with a thumbprint of '");
                    error.Append(clientCertificate.Thumbprint + "'.");
                }
            }

            throw new HalibutClientException(error.ToString(), lastError);
        }
    }
}
#endif