using System;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureClient
    {
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;
        readonly ILog log;
        readonly ConnectionPool<ServiceEndPoint, SecureConnection> pool;

        public SecureClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate, ILog log, ConnectionPool<ServiceEndPoint, SecureConnection> pool)
        {
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
            this.log = log;
            this.pool = pool;
        }

        public ServiceEndPoint ServiceEndpoint
        {
            get { return serviceEndpoint; }
        }

        public void ExecuteTransaction(Action<MessageExchangeProtocol> protocolHandler)
        {
            var retryInterval = HalibutLimits.RetryListeningSleepInterval;

            Exception lastError = null;

            // retryAllowed is also used to indicate if the error occurred before or after the connection was made
            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 5 && retryAllowed && watch.Elapsed < HalibutLimits.ConnectionErrorRetryTimeout; i++)
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
                    log.WriteException(EventType.Error, aex.Message, aex);
                    lastError = aex;
                    retryAllowed = false;
                    break;
                }
                catch (SocketException sex)
                {
                    log.WriteException(EventType.Error, sex.Message, sex);
                    lastError = sex;
                    // When the host is not found an immediate retry isn't going to help
                    if (sex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        break;
                    }
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    log.WriteException(EventType.Error, cex.Message, cex);
                    lastError = cex;
                    retryAllowed = true;
                    Thread.Sleep(retryInterval);
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, ex.Message, ex);
                    lastError = ex;
                    Thread.Sleep(retryInterval);
                }
            }

            HandleError(lastError, retryAllowed);
        }

        SecureConnection AcquireConnection()
        {
            var connection = pool.Take(serviceEndpoint);
            return connection ?? EstablishNewConnection();
        }

        SecureConnection EstablishNewConnection()
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");

            var remoteUri = serviceEndpoint.BaseUri;
            var certificateValidator = new ClientCertificateValidator(serviceEndpoint.RemoteThumbprint);
            var client = CreateTcpClient();
            client.ConnectWithTimeout(remoteUri, HalibutLimits.TcpClientConnectTimeout);
            log.Write(EventType.Diagnostic, "Connection established");

            var stream = client.GetStream();

            log.Write(EventType.Security, "Performing SSL (TLS 1.0) handshake");
            var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
            ssl.AuthenticateAsClient(remoteUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls, false);
            ssl.Write(MxLine, 0, MxLine.Length);
            ssl.Flush();

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}", client.Client.RemoteEndPoint, serviceEndpoint.RemoteThumbprint);

            var protocol = new MessageExchangeProtocol(ssl, log);
            return new SecureConnection(client, ssl, protocol);
        }

        void ReleaseConnection(SecureConnection connection)
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
                if ((inner.ErrorCode == 10053 || inner.ErrorCode == 10054) && retryAllowed)
                {
                    error.Append("The server aborted the connection before it was fully established. This usually means that the server rejected the certificate that we provided. We provided a certificate with a thumbprint of '");
                    error.Append(clientCertificate.Thumbprint + "'.");
                }
            }

            throw new HalibutClientException(error.ToString(), lastError);
        }

        static TcpClient CreateTcpClient()
        {
            var client = new TcpClient();
            client.SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds;
            client.ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds;
            return client;
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }
    }
}