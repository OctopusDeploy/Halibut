using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Transport
{
    public class WebSocketConnectionFactory : IConnectionFactory
    {
        readonly X509Certificate2 clientCertificate;

        public WebSocketConnectionFactory(X509Certificate2 clientCertificate)
        {
            this.clientCertificate = clientCertificate;
        }

        public IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log)
        {
            return EstablishNewConnection(exchangeProtocolBuilder, serviceEndpoint, log, CancellationToken.None);
        }

        public IConnection EstablishNewConnection(ExchangeProtocolBuilder exchangeProtocolBuilder, ServiceEndPoint serviceEndpoint, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");

            var client = CreateConnectedClient(serviceEndpoint, cancellationToken);

            log.Write(EventType.Diagnostic, "Connection established");

            var stream = new WebSocketStream(client);

            log.Write(EventType.Security, "Performing handshake");
            stream.WriteTextMessage("MX");

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}", serviceEndpoint.BaseUri, serviceEndpoint.RemoteThumbprint);

            return new SecureConnection(client, stream, exchangeProtocolBuilder, log);
        }

        ClientWebSocket CreateConnectedClient(ServiceEndPoint serviceEndpoint, CancellationToken cancellationToken)
        {
            if (!serviceEndpoint.IsWebSocketEndpoint)
                throw new Exception("Only wss:// endpoints are supported");
#if REQUIRES_SERVICE_POINT_MANAGER
            var connectionId = Guid.NewGuid().ToString();
#endif

            var client = new ClientWebSocket();
            client.Options.ClientCertificates = new X509Certificate2Collection(new X509Certificate2Collection(clientCertificate));
            client.Options.AddSubProtocol("Octopus");
#if REQUIRES_SERVICE_POINT_MANAGER
            client.Options.SetRequestHeader(ServerCertificateInterceptor.Header, connectionId);
#else
            client.Options.RemoteCertificateValidationCallback = new ClientCertificateValidator(serviceEndpoint).Validate;
#endif

            if (serviceEndpoint.Proxy != null)
                client.Options.Proxy = new WebSocketProxy(serviceEndpoint.Proxy);

            try
            {
#if REQUIRES_SERVICE_POINT_MANAGER
                ServerCertificateInterceptor.Expect(connectionId);
#endif
                using (var cts = new CancellationTokenSource(serviceEndpoint.TcpClientConnectTimeout))
                {
                    using (cancellationToken.Register(() => cts?.Cancel()))
                        client.ConnectAsync(serviceEndpoint.BaseUri, cts.Token)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                }
#if REQUIRES_SERVICE_POINT_MANAGER
                ServerCertificateInterceptor.Validate(connectionId, serviceEndpoint);
#endif
            }
            catch
            {
                if (client.State == WebSocketState.Open)
                    using (var sendCancel = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                        client.CloseAsync(WebSocketCloseStatus.ProtocolError, "Certificate thumbprint not recognised", sendCancel.Token)
                            .ConfigureAwait(false).GetAwaiter().GetResult();

                client.Dispose();
                throw;
            }
#if REQUIRES_SERVICE_POINT_MANAGER
            finally
            {
                ServerCertificateInterceptor.Remove(connectionId);
            }
#endif

            return client;
        }
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