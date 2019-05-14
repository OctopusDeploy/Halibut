#if SUPPORTS_WEB_SOCKET_CLIENT
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

        public IConnection EstablishNewConnection(ServiceEndPoint serviceEndpoint, ILog log)
        {
            log.Write(EventType.OpeningNewConnection, "Opening a new connection");

            var client = CreateConnectedClient(serviceEndpoint);

            log.Write(EventType.Diagnostic, "Connection established");

            var stream = new WebSocketStream(client);

            log.Write(EventType.Security, "Performing handshake");
            stream.WriteTextMessage("MX");

            log.Write(EventType.Security, "Secure connection established. Server at {0} identified by thumbprint: {1}", serviceEndpoint.BaseUri, serviceEndpoint.RemoteThumbprint);

            var protocol = new MessageExchangeProtocol(stream, log);
            return new SecureConnection(client, stream, protocol);
        }

        
        ClientWebSocket CreateConnectedClient(ServiceEndPoint serviceEndpoint)
        {
            if (!serviceEndpoint.IsWebSocketEndpoint)
                throw new Exception("Only wss:// endpoints are supported");

            var connectionId = Guid.NewGuid().ToString();

            var client = new ClientWebSocket();
            client.Options.ClientCertificates = new X509Certificate2Collection(new X509Certificate2Collection(clientCertificate));
            client.Options.AddSubProtocol("Octopus");
            client.Options.SetRequestHeader(ServerCertificateInterceptor.Header, connectionId);
            if (serviceEndpoint.Proxy != null)
                client.Options.Proxy = new WebSocketProxy(serviceEndpoint.Proxy);

            try
            {
                ServerCertificateInterceptor.Expect(connectionId);
                using (var cts = new CancellationTokenSource(serviceEndpoint.TcpClientConnectTimeout))
                    client.ConnectAsync(serviceEndpoint.BaseUri, cts.Token)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                ServerCertificateInterceptor.Validate(connectionId, serviceEndpoint);
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
#endif