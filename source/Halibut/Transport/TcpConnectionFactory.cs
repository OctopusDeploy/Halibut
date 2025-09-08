using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;
using Halibut.Transport.Streams;

namespace Halibut.Transport
{
    public class TcpConnectionFactory : IConnectionFactory
    {
        static readonly byte[] MxLine = Encoding.ASCII.GetBytes("MX" + Environment.NewLine + Environment.NewLine);

        readonly X509Certificate2 clientCertificate;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IStreamFactory streamFactory;
        readonly ISecureConnectionObserver secureConnectionObserver;

        public TcpConnectionFactory(
            X509Certificate2 clientCertificate,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            IStreamFactory streamFactory,
            ISecureConnectionObserver secureConnectionObserver
        )
        {
            this.clientCertificate = clientCertificate;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.streamFactory = streamFactory;
            this.secureConnectionObserver = secureConnectionObserver;
        }
        
        public async Task<IConnection> EstablishNewConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, $"Opening a new connection to {serviceEndpoint.BaseUri}");

            var certificateValidator = new ClientCertificateValidator(serviceEndpoint);
            var client = await CreateConnectedTcpClientAsync(serviceEndpoint, halibutTimeoutsAndLimits, streamFactory, log, cancellationToken);
            log.Write(EventType.Diagnostic, $"Connection established to {client.Client.RemoteEndPoint} for {serviceEndpoint.BaseUri}");
            
            var networkTimeoutStream = streamFactory.CreateStream(client);

            client.ConfigureTcpOptions(halibutTimeoutsAndLimits);

            var ssl = new SslStream(networkTimeoutStream, false, certificateValidator.Validate, UserCertificateSelectionCallback);

            log.Write(EventType.SecurityNegotiation, "Performing TLS handshake");

#if NETFRAMEWORK
        // TODO: ASYNC ME UP!
        // AuthenticateAsClientAsync in .NET 4.8 does not support cancellation tokens. So `cancellationToken` is not respected here.
        await ssl.AuthenticateAsClientAsync(
            serviceEndpoint.BaseUri.Host,
            new X509Certificate2Collection(clientCertificate),
            SslConfiguration.SupportedProtocols,
            false);
#else
            await ssl.AuthenticateAsClientEnforcingTimeout(serviceEndpoint, new X509Certificate2Collection(clientCertificate), cancellationToken);
#endif

            await ssl.WriteAsync(MxLine, 0, MxLine.Length, cancellationToken);
            await ssl.FlushAsync(cancellationToken);

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}, using protocol {2}", client.Client.RemoteEndPoint, serviceEndpoint.RemoteThumbprint, ssl.SslProtocol.ToString());
            secureConnectionObserver.SecureConnectionEstablished(
                new SecureConnectionInfo
                {
                    ConnectionDirection = ConnectionDirection.Outgoing,
                    SslProtocols = ssl.SslProtocol
                }
            );

            return new SecureConnection(client, ssl, exchangeProtocolBuilder, halibutTimeoutsAndLimits, log);
        }
        
        internal static async Task<TcpClient> CreateConnectedTcpClientAsync(ServiceEndPoint endPoint, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, IStreamFactory streamFactory, ILog log, CancellationToken cancellationToken)
        {
            TcpClient client;
            if (endPoint.Proxy is null)
            {
                client = CreateTcpClientAsync(halibutTimeoutsAndLimits);
                await client.ConnectWithTimeoutAsync(endPoint.BaseUri, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            else
            {
                log.Write(EventType.Diagnostic, "Creating a proxy client");
                
                client = await new ProxyClientFactory(streamFactory)
                    .CreateProxyClient(log, endPoint.Proxy)
                    .WithTcpClientFactory(() => CreateTcpClientAsync(halibutTimeoutsAndLimits))
                    .CreateConnectionAsync(endPoint.BaseUri.Host, endPoint.BaseUri.Port, endPoint.TcpClientConnectTimeout, cancellationToken);
            }
            return client;
        }

        internal static TcpClient CreateTcpClientAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var addressFamily = Socket.OSSupportsIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork;

            return CreateTcpClientAsync(addressFamily, halibutTimeoutsAndLimits);
        }
        
        internal static TcpClient CreateTcpClientAsync(AddressFamily addressFamily, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var client = new TcpClient(addressFamily)
            {
                SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.SendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout.TotalMilliseconds
            };

            if (client.Client.AddressFamily == AddressFamily.InterNetworkV6)
            {
                client.Client.DualMode = true;
            }
            return client;
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate? remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }
    }
}
