using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Halibut.Client;
using Halibut.Protocol;

namespace Halibut.Services
{
    public class SecureClient
    {
        static readonly byte[] mxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);
        readonly ServiceEndPoint serviceEndpoint;
        readonly X509Certificate2 clientCertificate;

        public SecureClient(ServiceEndPoint serviceEndpoint, X509Certificate2 clientCertificate)
        {
            this.serviceEndpoint = serviceEndpoint;
            this.clientCertificate = clientCertificate;
        }

        public void Connect(Action<MessageExchangeProtocol> protocolHandler)
        {
            try
            {
                var remoteUri = serviceEndpoint.BaseUri;
                var client = CreateTcpClient(remoteUri);
                var certificateValidator = new ClientCertificateValidator(serviceEndpoint.RemoteThumbprint);

                var stream = client.GetStream();
                var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
                ssl.AuthenticateAsClient(remoteUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls, false);

                ssl.Write(mxLine, 0, mxLine.Length);

                var protocol = new MessageExchangeProtocol(ssl);
                protocolHandler(protocol);
            }
            catch (IOException ioex)
            {
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
                throw new JsonRpcException("We aborted the connection because the remote host was not authenticated. This happens whtn the remote host presents a different certificate to the one we expected.", aex);
            }
        }

        static TcpClient CreateTcpClient(Uri uri)
        {
            var client = new TcpClient();
            client.Connect(uri.Host, uri.Port);
            client.SendTimeout = 20*60*1000;
            client.ReceiveTimeout = 20*60*1000;
            return client;
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

                return false;
            }
        }
    }
}