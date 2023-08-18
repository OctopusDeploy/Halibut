using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut.Transport
{
    public class TcpConnectionFactory : IConnectionFactory
    {
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);

        readonly X509Certificate2 clientCertificate;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;

        public TcpConnectionFactory(X509Certificate2 clientCertificate, AsyncHalibutFeature asyncHalibutFeature, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.clientCertificate = clientCertificate;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.asyncHalibutFeature = asyncHalibutFeature;
        }

        [Obsolete]
        public IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, $"Opening a new connection to {serviceEndpoint.BaseUri}");

            var certificateValidator = new ClientCertificateValidator(serviceEndpoint);
            var client = CreateConnectedTcpClient(serviceEndpoint, log, cancellationToken);
            log.Write(EventType.Diagnostic, $"Connection established to {client.Client.RemoteEndPoint} for {serviceEndpoint.BaseUri}");

            var stream = client.GetStream();

            log.Write(EventType.SecurityNegotiation, "Performing TLS handshake");
            var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback);
            ssl.AuthenticateAsClient(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
            ssl.Write(MxLine, 0, MxLine.Length);
            ssl.Flush();

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}, using protocol {2}", client.Client.RemoteEndPoint, serviceEndpoint.RemoteThumbprint, ssl.SslProtocol.ToString());

            return new SecureConnection(client, ssl, exchangeProtocolBuilder, asyncHalibutFeature, halibutTimeoutsAndLimits, log);
        }
        
        public async Task<IConnection> EstablishNewConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, $"Opening a new connection to {serviceEndpoint.BaseUri}");

            var certificateValidator = new ClientCertificateValidator(serviceEndpoint);
            var client = await CreateConnectedTcpClientAsync(serviceEndpoint, halibutTimeoutsAndLimits, log, cancellationToken);
            log.Write(EventType.Diagnostic, $"Connection established to {client.Client.RemoteEndPoint} for {serviceEndpoint.BaseUri}");
            
            var networkStream = client.GetStream().AsNetworkTimeoutStream();
            var ssl = new SslStream(networkStream, false, certificateValidator.Validate, UserCertificateSelectionCallback);

            log.Write(EventType.SecurityNegotiation, "Performing TLS handshake");

#if NETFRAMEWORK
            // TODO: ASYNC ME UP!
            // AuthenticateAsClientAsync in .NET 4.8 does not support cancellation tokens. So `cancellationToken` is not respected here.
            await ssl.AuthenticateAsClientAsync(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
#else
            await ssl.AuthenticateAsClientEnforcingTimeout(serviceEndpoint, new X509Certificate2Collection(clientCertificate), cancellationToken);
#endif

            await ssl.WriteAsync(MxLine, 0, MxLine.Length, cancellationToken);
            await ssl.FlushAsync(cancellationToken);

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}, using protocol {2}", client.Client.RemoteEndPoint, serviceEndpoint.RemoteThumbprint, ssl.SslProtocol.ToString());

            return new SecureConnection(client, ssl, exchangeProtocolBuilder, asyncHalibutFeature, halibutTimeoutsAndLimits, log);
        }

        [Obsolete]
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
        
        internal static async Task<TcpClient> CreateConnectedTcpClientAsync(ServiceEndPoint endPoint, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log, CancellationToken cancellationToken)
        {
            TcpClient client;
            if (endPoint.Proxy == null)
            {
                client = CreateTcpClientAsync(halibutTimeoutsAndLimits);
                await client.ConnectWithTimeoutAsync(endPoint.BaseUri, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            else
            {
                log.Write(EventType.Diagnostic, "Creating a proxy client");
                
                client = await new ProxyClientFactory()
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(() => CreateTcpClientAsync(halibutTimeoutsAndLimits))
                    .CreateConnectionAsync(endPoint.BaseUri.Host, endPoint.BaseUri.Port, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            return client;
        }

        [Obsolete]
        internal static TcpClient CreateTcpClient()
        {
            var addressFamily = Socket.OSSupportsIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork;

            return CreateTcpClient(addressFamily);
        }

        internal static TcpClient CreateTcpClientAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var addressFamily = Socket.OSSupportsIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork;

            return CreateTcpClientAsync(addressFamily, halibutTimeoutsAndLimits);
        }

        [Obsolete]
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
        
        internal static TcpClient CreateTcpClientAsync(AddressFamily addressFamily, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var client = new TcpClient(addressFamily)
            {
                SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientSendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientReceiveTimeout.TotalMilliseconds
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