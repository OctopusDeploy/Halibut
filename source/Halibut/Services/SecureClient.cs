using System;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Client;
using Halibut.Diagnostics;
using Halibut.Protocol;
using Halibut.Server;

namespace Halibut.Services
{
    public interface IConnectionTransactionLog
    {
        void AppendLine(string text);
    }

    public class SecureClient
    {
        static readonly SecureClientConnectionPool Pool = new SecureClientConnectionPool();
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;

        public SecureClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate)
        {
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
        }

        public void Connect(IConnectionTransactionLog log, Action<MessageExchangeProtocol> protocolHandler)
        {
            var retryInterval = HalibutLimits.TimeToSleepBetweenConnectionRetryAttemptsWhenCallingListeningEndpoint;

            Exception lastError = null;

            var retryAllowed = true;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 5 && retryAllowed && watch.Elapsed < HalibutLimits.MaximumTimeToRetryAnyFormOfNetworkCommunicationWhenCallingListeningEndPoint; i++)
            {
                try
                {
                    lastError = null;

                    var connection = Pool.Take(serviceEndpoint) ?? EstablishNewConnection(log);

                    // Beyond this point, we have no way to be certain that the server hasn't tried to process a request; therefore, we can't retry after this point
                    retryAllowed = false;

                    protocolHandler(connection.Protocol);

                    // Only return the connection to the pool if all went well
                    Pool.Return(serviceEndpoint, connection);
                }
                catch (AuthenticationException aex)
                {
                    lastError = aex;
                    retryAllowed = false;
                }
                catch (ConnectionInitializationFailedException cex)
                {
                    lastError = cex;
                    retryAllowed = true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(retryInterval);
                }
            }

            HandleError(lastError, retryAllowed);
        }

        SecureConnection EstablishNewConnection(IConnectionTransactionLog log)
        {
            log.AppendLine("Establishing a fresh connection");
            var remoteUri = serviceEndpoint.BaseUri;
            var certificateValidator = new ClientCertificateValidator(serviceEndpoint.RemoteThumbprint);
            var client = CreateTcpClient();
            ConnectWithTimeout(client, remoteUri);

            var stream = client.GetStream();
            var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
            ssl.AuthenticateAsClient(remoteUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls, false);
            ssl.Write(MxLine, 0, MxLine.Length);

            var protocol = new MessageExchangeProtocol(ssl);
            return new SecureConnection(client, ssl, protocol);
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
            client.SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalSeconds;
            client.ReceiveTimeout = (int) HalibutLimits.TcpClientReceiveTimeout.TotalSeconds;
            return client;
        }

        static void ConnectWithTimeout(TcpClient client, Uri remoteUri)
        {
            var connectResult = client.BeginConnect(remoteUri.Host, remoteUri.Port, ar => { }, null);
            if (!connectResult.AsyncWaitHandle.WaitOne(HalibutLimits.TcpClientConnectTimeout))
            {
                try
                {
                    client.Close();
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                throw new Exception("The client was unable to establish the initial connection within " + HalibutLimits.TcpClientConnectTimeout);
            }

            client.EndConnect(connectResult);
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }

        class ClientCertificateValidator
        {
            readonly string expectedThumbprint;

            public ClientCertificateValidator(string expectedThumbprint)
            {
                this.expectedThumbprint = expectedThumbprint;
            }

            public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
            {
                var provided = new X509Certificate2(certificate).Thumbprint;

                if (provided == expectedThumbprint)
                {
                    return true;
                }

                throw new AuthenticationException(string.Format("The server presented an unexpected security certificate. We expected the server to present a certificate with the thumbprint '{0}'. Instead, it presented a certificate with a thumbprint of '{1}'. This usually happens when the client has been configured to expect the server to have the wrong certificate, or when the certificate on the server has been regenerated and the client has not been updated. It may also happen if someone is performing a man-in-the-middle attack on the remote machine, or if a proxy server is intercepting requests. Please check the certificate used on the server, and verify that the client has been configured correctly.", expectedThumbprint, provided));
            }
        }
    }
}