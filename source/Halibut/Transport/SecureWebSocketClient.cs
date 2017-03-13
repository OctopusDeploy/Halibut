#if HAS_WEB_SOCKET_LISTENER
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Transport
{
    public class SecureWebSocketClient : ISecureClient
    {
        public const int RetryCountLimit = 5;
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;
        readonly ILog log;
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool;

        public SecureWebSocketClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionPool<ServiceEndPoint, IConnection> pool)
        {
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.pool = pool;
        }

        public ServiceEndPoint ServiceEndpoint => serviceEndpoint;

        public void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler)
        {
            var retryInterval = HalibutLimits.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < RetryCountLimit && retryAllowed && watch.Elapsed < HalibutLimits.ConnectionErrorRetryTimeout; i++)
            {
                if (i > 0) log.Write(EventType.Error, "Retry attempt {0}", i);

                try
                {
                    lastError = null;

                    var connection = AcquireConnection();

                    // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                    retryAllowed = false;

                    protocolHandler(connection.Protocol);

                    // Only return the connection to the pool if all went well
                    ReleaseConnection(connection);
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
                        pool.Clear(serviceEndpoint, log);
                    }

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

        IConnection AcquireConnection()
        {
            var connection = pool.Take(serviceEndpoint);
            return connection ?? EstablishNewConnection();
        }

        SecureConnection EstablishNewConnection()
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");

            var client = CreateConnectedClient(serviceEndpoint);
            
            log.Write(EventType.Diagnostic, "Connection established");
            
            var stream = new WebSocketStream(client);

            log.Write(EventType.Security, "Performing handshake");
            stream.WriteTextMessage("MX");

            var protocol = new MessageExchangeProtocol(stream, log);
            return new SecureConnection(client, stream, protocol);
        }

        ClientWebSocket CreateConnectedClient(ServiceEndPoint endPoint)
        {
            if(!endPoint.BaseUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only wss:// endpoints are supported");

            var connectionId = Guid.NewGuid().ToString();

            var client = new ClientWebSocket();
            client.Options.ClientCertificates = new X509Certificate2Collection(new X509Certificate2Collection(clientCertificate));
            client.Options.AddSubProtocol("Octopus");
            client.Options.SetRequestHeader(ServerCertificateInterceptor.Header, connectionId);
            if (endPoint.Proxy != null)
                client.Options.Proxy = new WebSocketProxy(endPoint.Proxy);

            try
            {
                ServerCertificateInterceptor.Expect(connectionId);
                using (var cts = new CancellationTokenSource(HalibutLimits.TcpClientConnectTimeout))
                    client.ConnectAsync(endPoint.BaseUri, cts.Token)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                ServerCertificateInterceptor.Validate(connectionId, endPoint);
            }
            finally
            {
                ServerCertificateInterceptor.Remove(connectionId);
            }

            
            return client;
        }


        void ReleaseConnection(IConnection connection)
        {
            pool.Return(serviceEndpoint, connection);
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

 class WebSocketProxy : IWebProxy
        {
            readonly Uri uri;
            public WebSocketProxy(ProxyDetails proxy)
            {
                if (proxy.Type != ProxyType.HTTP)
                    throw new ProxyException(string.Format("Unknown proxy type {0}.", proxy.Type.ToString()));

                uri = new Uri($"http://{proxy.Host}:{proxy.Port}");

                if (string.IsNullOrWhiteSpace(proxy.UserName) && string.IsNullOrEmpty(proxy.Password))
                    return;

                Credentials = new NetworkCredential(proxy.UserName, proxy.Password);
            }

            public Uri GetProxy(Uri destination) => uri;

            public bool IsBypassed(Uri host) => false;

            public ICredentials Credentials { get; set; }
        }
    }
}
#endif