using System;
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
    public class TcpConnectionFactory : IConnectionFactory
    {
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);

        readonly X509Certificate2 clientCertificate;

        public TcpConnectionFactory(X509Certificate2 clientCertificate)
        {
            this.clientCertificate = clientCertificate;
        }

        public IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log)
        {
            return EstablishNewConnection(exchangeProtocolBuilder, serviceEndpoint, log, CancellationToken.None);
        }

        public IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, $"Opening a new connection to {serviceEndpoint.BaseUri}");

            var certificateValidator = new ClientCertificateValidator(serviceEndpoint);
            var client = CreateConnectedTcpClient(serviceEndpoint, log, cancellationToken);
            log.Write(EventType.Diagnostic, $"Connection established to {client.Client.RemoteEndPoint} for {serviceEndpoint.BaseUri}");

            var stream = client.GetStream();
            
            MessageExchangeStream.SetSuperShortTimeouts(stream);

            log.Write(EventType.SecurityNegotiation, "Performing TLS handshake");
            var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
            ssl.AuthenticateAsClient(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
            ssl.Write(MxLine, 0, MxLine.Length);
            ssl.Flush();

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}, using protocol {2}", client.Client.RemoteEndPoint, serviceEndpoint.RemoteThumbprint, ssl.SslProtocol.ToString());
            
            MessageExchangeStream.SetNormalTimeouts(stream);

            return new SecureConnection(client, ssl, exchangeProtocolBuilder, log);
        }

        internal static TcpClient CreateConnectedTcpClient(ServiceEndPoint endPoint, ILog log, CancellationToken cancellationToken)
        {
            TcpClient client;
            if (endPoint.Proxy == null)
            {
                client = CreateTcpClient();
                client.ConnectWithTimeout(endPoint.BaseUri, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            else
            {
                log.Write(EventType.Diagnostic, "Creating a proxy client");

                client = new ProxyClientFactory()
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(CreateTcpClient)
                    .CreateConnection(endPoint.BaseUri.Host, endPoint.BaseUri.Port, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            return client;
        }

        internal static TcpClient CreateTcpClient()
        {
            var addressFamily = Socket.OSSupportsIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork;

            return CreateTcpClient(addressFamily);
        }

        internal static TcpClient CreateTcpClient(AddressFamily addressFamily)
        {
            var client = new TcpClient(addressFamily)
            {
                SendTimeout = (int)HalibutLimits.TcpClientSendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)HalibutLimits.TcpClientReceiveTimeout.TotalMilliseconds
            };

            if (client.Client.AddressFamily == AddressFamily.InterNetworkV6)
            {
                client.Client.DualMode = true;
            }
            return client;
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }
    }
}