using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Halibut.Client;
using Halibut.Protocol;

namespace Halibut.Services
{
    public class SecureClient : IMessageExchangeParticipant
    {
        readonly Uri subscriptionName;
        readonly ServiceEndPoint serviceEndpoint;
        readonly IPendingRequestQueue pending;
        readonly X509Certificate2 clientCertificate;
        readonly Func<RequestMessage, ResponseMessage> serviceInvoker;
        Stream currentConnection;
        MessageExchangeProtocol protocol;

        public SecureClient(Uri subscriptionName, ServiceEndPoint serviceEndpoint, IPendingRequestQueue pending, X509Certificate2 clientCertificate, Func<RequestMessage, ResponseMessage> serviceInvoker)
        {
            this.subscriptionName = subscriptionName;
            this.serviceEndpoint = serviceEndpoint;
            this.pending = pending;
            this.clientCertificate = clientCertificate;
            this.serviceInvoker = serviceInvoker;
        }

        public bool IsEmpty { get { return pending.IsEmpty; } }

        public int PerformExchange()
        {
            //if (currentConnection == null)
            {
                OpenConnection();
            }

            var x = protocol.ExchangeAsClient(currentConnection);

            if (currentConnection != null) currentConnection.Dispose();
            return x;
        }

        void OpenConnection()
        {
            try
            {
                var uri = serviceEndpoint.BaseUri;
                var client = new TcpClient();
                client.Connect(uri.Host, uri.Port);
                client.SendTimeout = 20*60*1000;
                client.ReceiveTimeout = 20*60*1000;

                var certificateValidator = new ClientCertificateValidator(serviceEndpoint.RemoteThumbprint);

                var stream = client.GetStream();
                var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
                protocol = new MessageExchangeProtocol(this, serviceInvoker);

                ssl.AuthenticateAsClient(uri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls, false);

                currentConnection = ssl;

                var writer = new StreamWriter(ssl);
                writer.WriteLine("MX");
                writer.WriteLine();
                writer.Flush();

                protocol.IdentifyAsClient(subscriptionName, ssl);
            }
            catch (IOException ioex)
            {
                currentConnection = null;
                var inner = ioex.InnerException as SocketException;
                if (inner != null)
                {
                    if (inner.ErrorCode == 10053 || inner.ErrorCode == 10054)
                    {
                        throw new JsonRpcException("The remote host aborted the connection. This can happen when the remote server does not trust the certificate that we provided.", ioex);
                    }
                }

                throw;
            }
            catch (AuthenticationException aex)
            {
                currentConnection = null;
                throw new JsonRpcException("We aborted the connection because the remote host was not authenticated. This happens whtn the remote host presents a different certificate to the one we expected.", aex);
            }
            catch
            {
                currentConnection = null;
            }
        }

        IPendingRequestQueue IMessageExchangeParticipant.SelectQueue(IdentificationMessage clientIdentification)
        {
            return pending;
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }

        #region Nested type: ClientCertificateValidator

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

                return false;
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                if (currentConnection != null)
                {
                    currentConnection.Dispose();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}