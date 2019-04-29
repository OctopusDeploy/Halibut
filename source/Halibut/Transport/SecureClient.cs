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
        [Obsolete("Replaced by HalibutLimits.RetryCountLimit")]
        public const int RetryCountLimit = 5;
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);
        readonly X509Certificate2 clientCertificate;
        readonly ILog log;
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool;

        public SecureClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionPool<ServiceEndPoint, IConnection> pool)
        {
            this.ServiceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.pool = pool;
        }

        public ServiceEndPoint ServiceEndpoint { get; }

        public void ExecuteTransaction(Func<MessageExchangeProtocol, bool> protocolHandler)
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
                    var keepConnection = false;
                    try
                    {
                        connection = AcquireConnection();

                        // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                        retryAllowed = false;

                        keepConnection = protocolHandler(connection.Protocol);
                    }
                    finally 
                    {
                        if(keepConnection)
                            ReleaseConnection(connection);
                        else
                            connection?.Dispose();
                    }
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
                        pool.Clear(ServiceEndpoint, log);
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

        IConnection AcquireConnection()
        {
            var connection = pool.Take(ServiceEndpoint);
            return connection ?? EstablishNewConnection();
        }

        SecureConnection EstablishNewConnection()
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");

            var certificateValidator = new ClientCertificateValidator(ServiceEndpoint);
            var client = CreateConnectedTcpClient(ServiceEndpoint);
            log.Write(EventType.Diagnostic, "Connection established");

            var stream = client.GetStream();

            log.Write(EventType.SecurityNegotiation, "Performing TLS handshake");
            var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
            ssl.AuthenticateAsClient(ServiceEndpoint.BaseUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
            ssl.Write(MxLine, 0, MxLine.Length);
            ssl.Flush();

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}, using protocol {2}", client.Client.RemoteEndPoint, ServiceEndpoint.RemoteThumbprint, ssl.SslProtocol.ToString());

            var protocol = new MessageExchangeProtocol(ssl, log);
            return new SecureConnection(client, ssl, protocol);
        }

        TcpClient CreateConnectedTcpClient(ServiceEndPoint endPoint)
        {
            TcpClient client;
            if (endPoint.Proxy == null)
            {
                client = CreateTcpClient();
                client.ConnectWithTimeout(endPoint.BaseUri, endPoint.TcpClientConnectTimeout);
            }
            else
            {
                log.Write(EventType.Diagnostic, "Creating a proxy client");
                client = new ProxyClientFactory()
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(CreateTcpClient)
                    .CreateConnection(endPoint.BaseUri.Host, endPoint.BaseUri.Port, endPoint.TcpClientConnectTimeout);
            }
            return client;
        }

        void ReleaseConnection(IConnection connection)
        {
            pool.Return(ServiceEndpoint, connection);
        }

        void HandleError(Exception lastError, bool retryAllowed)
        {
            if (lastError == null)
                return;

            lastError = lastError.UnpackFromContainers();

            var error = new StringBuilder();
            error.Append("An error occurred when sending a request to '").Append(ServiceEndpoint.BaseUri).Append("', ");
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

        static TcpClient CreateTcpClient()
        {
            var client = new TcpClient(AddressFamily.InterNetworkV6)
            {
                SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds,
                Client = { DualMode = true }
            };
            return client;
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }
    }
}
