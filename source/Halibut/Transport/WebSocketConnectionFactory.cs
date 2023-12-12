#if SUPPORTS_WEB_SOCKET_CLIENT
using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;
using Halibut.Transport.Proxy.Exceptions;
using Halibut.Transport.Streams;

namespace Halibut.Transport
{
    public class WebSocketConnectionFactory : IConnectionFactory
    {
        readonly X509Certificate2 clientCertificate;
        readonly IStreamFactory streamFactory;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;

        public WebSocketConnectionFactory(X509Certificate2 clientCertificate,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, IStreamFactory streamFactory)
        {
            this.clientCertificate = clientCertificate;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.streamFactory = streamFactory;
        }
        
        public async Task<IConnection> EstablishNewConnectionAsync(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");
            
            var client = await CreateConnectedClientAsync(serviceEndpoint, cancellationToken);

            log.Write(EventType.Diagnostic, "Connection established");
            var stream = streamFactory.CreateStream(client);

            log.Write(EventType.Security, "Performing handshake");
            await client.WriteTextMessage("MX", halibutTimeoutsAndLimits.TcpClientTimeout.SendTimeout, cancellationToken);

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}", serviceEndpoint.BaseUri, serviceEndpoint.RemoteThumbprint);
            
            return new SecureConnection(client, stream, exchangeProtocolBuilder, halibutTimeoutsAndLimits, log);
        }
        
        async Task<ClientWebSocket> CreateConnectedClientAsync(ServiceEndPoint serviceEndpoint, CancellationToken cancellationToken)
        {
            if (!serviceEndpoint.IsWebSocketEndpoint)
                throw new Exception("Only wss:// endpoints are supported");

            var connectionId = Guid.NewGuid().ToString();

            var client = new ClientWebSocket();
            client.Options.ClientCertificates = new X509Certificate2Collection(new X509Certificate2Collection(clientCertificate));
            client.Options.AddSubProtocol("Octopus");
            client.Options.SetRequestHeader(ServerCertificateInterceptor.Header, connectionId);
            if (serviceEndpoint.Proxy is not null)
                client.Options.Proxy = new WebSocketProxy(serviceEndpoint.Proxy);

            try
            {
                ServerCertificateInterceptor.Expect(connectionId);
                using (var cts = new CancellationTokenSource(serviceEndpoint.TcpClientConnectTimeout))
                {
                    using (cancellationToken.Register(() => cts?.Cancel()))
                    {
                        await client.ConnectAsync(serviceEndpoint.BaseUri, cts.Token);
                    }
                }
                ServerCertificateInterceptor.Validate(connectionId, serviceEndpoint);
            }
            catch
            {
                if (client.State == WebSocketState.Open)
                    using (var sendCancel = new CancellationTokenSource(TimeSpan.FromSeconds(1))) {
                        await client.CloseAsync(WebSocketCloseStatus.ProtocolError, "Certificate thumbprint not recognised", sendCancel.Token);
                    }
                
                client.Dispose();
                throw;
            }
            finally
            {
                ServerCertificateInterceptor.Remove(connectionId);
            }

            return client;
        }
    }

    class WebSocketProxy : IWebProxy
    {
        readonly Uri uri;

        public WebSocketProxy(ProxyDetails proxy)
        {
            if (proxy.Type != ProxyType.HTTP)
                throw new ProxyException(string.Format("Unknown proxy type {0}.", proxy.Type.ToString()), false);

            uri = new Uri($"http://{proxy.Host}:{proxy.Port}");

            if (string.IsNullOrWhiteSpace(proxy.UserName) && string.IsNullOrEmpty(proxy.Password))
                return;

            Credentials = new NetworkCredential(proxy.UserName, proxy.Password);
        }

        public Uri GetProxy(Uri destination) => uri;

        public bool IsBypassed(Uri host) => false;

        public ICredentials? Credentials { get; set; }
    }
}
#endif