using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut
{
    public class HalibutRuntime : IHalibutRuntime
    {
        public static readonly string DefaultFriendlyHtmlPageContent = "<html><body><p>Hello!</p></body></html>";
        readonly ConcurrentDictionary<Uri, PendingRequestQueue> queues = new ConcurrentDictionary<Uri, PendingRequestQueue>();
        readonly X509Certificate2 serverCertificate;
        readonly List<IDisposable> listeners = new List<IDisposable>();
        readonly ITrustProvider trustProvider; 
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new ConcurrentDictionary<Uri, ServiceEndPoint>();
        readonly ServiceInvoker invoker;
        readonly LogFactory logs = new LogFactory();
        readonly ConnectionManager connectionManager = new ConnectionManager();
        readonly PollingClientCollection pollingClients = new PollingClientCollection();
        string friendlyHtmlPageContent = DefaultFriendlyHtmlPageContent;
        Dictionary<string, string> friendlyHtmlPageHeaders = new Dictionary<string, string>();

        public HalibutRuntime(X509Certificate2 serverCertificate) : this(new NullServiceFactory(), serverCertificate, new DefaultTrustProvider())
        {
        }

        public HalibutRuntime(X509Certificate2 serverCertificate, ITrustProvider trustProvider) : this(new NullServiceFactory(), serverCertificate, trustProvider)
        {
        }

        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate) : this(serviceFactory, serverCertificate, new DefaultTrustProvider())
        {
        }

        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate, ITrustProvider trustProvider)
        {
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            invoker = new ServiceInvoker(serviceFactory);
        }

        public ILogFactory Logs => logs;

        public Func<string, string, UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }

        PendingRequestQueue GetQueue(Uri target)
        {
            return queues.GetOrAdd(target, u => new PendingRequestQueue(logs.ForEndpoint(target)));
        }

        public int Listen()
        {
            return Listen(0);
        }

        public int Listen(int port)
        {
            var ipAddress = Socket.OSSupportsIPv6
                ? IPAddress.IPv6Any
                : IPAddress.Any;

            return Listen(new IPEndPoint(ipAddress, port));
        }

        public int Listen(IPEndPoint endpoint)
        {
            var listener = new SecureListener(endpoint, serverCertificate, ListenerHandler, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect);
            listeners.Add(listener);
            return listener.Start();
        }

        public void ListenWebSocket(string endpoint)
        {
            var listener = new SecureWebSocketListener(endpoint, serverCertificate, ListenerHandler, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect);
            listeners.Add(listener);
            listener.Start();
        }

        Task ListenerHandler(MessageExchangeProtocol obj)
        {
            return obj.ExchangeAsServerAsync(
                HandleIncomingRequest,
                id => GetQueue(id.SubscriptionId));
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint)
        {
            Poll(subscription, endPoint, CancellationToken.None);
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint, CancellationToken cancellationToken)
        {
            ISecureClient client;
            var log = logs.ForEndpoint(endPoint.BaseUri);
            if (endPoint.IsWebSocketEndpoint)
            {
#if SUPPORTS_WEB_SOCKET_CLIENT
                client = new SecureWebSocketClient(endPoint, serverCertificate, log, connectionManager);
#else
                throw new NotSupportedException("The netstandard build of this library cannot act as the client in a WebSocket polling setup");
#endif
            }
            else
            {
                client = new SecureClient(endPoint, serverCertificate, log, connectionManager);
            }
            pollingClients.Add(new PollingClient(subscription, client, HandleIncomingRequest, log, cancellationToken));
        }

        public ServiceEndPoint Discover(Uri uri)
        {
            return Discover(uri, CancellationToken.None);
        }
        
        public ServiceEndPoint Discover(Uri uri, CancellationToken cancellationToken)
        {
            return Discover(new ServiceEndPoint(uri, null), cancellationToken);
        }

        public ServiceEndPoint Discover(ServiceEndPoint endpoint)
        {
            return Discover(endpoint, CancellationToken.None);
        }
        
        public ServiceEndPoint Discover(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var client = new DiscoveryClient();
            return client.Discover(endpoint, cancellationToken);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprint), CancellationToken.None);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint, CancellationToken cancellationToken)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprint), cancellationToken);
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint)
        {
            return CreateClient<TService>(endpoint, CancellationToken.None);
        }
        
        public TService CreateClient<TService>(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
#if HAS_REAL_PROXY
            return (TService)new HalibutProxy(SendOutgoingRequest, typeof(TService), endpoint, cancellationToken).GetTransparentProxy();
#else
            var proxy = DispatchProxy.Create<TService, HalibutProxy>();
            (proxy as HalibutProxy).Configure(SendOutgoingRequest, typeof(TService), endpoint, cancellationToken);
            return proxy;
#endif
        }

        ResponseMessage SendOutgoingRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var endPoint = request.Destination;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    return SendOutgoingHttpsRequest(request, cancellationToken);
                case "poll":
                    return SendOutgoingPollingRequest(request, cancellationToken);
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }
        }

        ResponseMessage SendOutgoingHttpsRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var client = new SecureListeningClient(request.Destination, serverCertificate, logs.ForEndpoint(request.Destination.BaseUri), connectionManager);

            ResponseMessage response = null;
            client.ExecuteTransaction(protocol =>
            {
                response = protocol.ExchangeAsClient(request);
            }, cancellationToken);
            return response;
        }

        ResponseMessage SendOutgoingPollingRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return queue.QueueAndWait(request, cancellationToken);
        }

        ResponseMessage HandleIncomingRequest(RequestMessage request)
        {
            return invoker.Invoke(request);
        }

        public void Trust(string clientThumbprint)
        {
            trustProvider.Add(clientThumbprint);
        }

        public void RemoveTrust(string clientThumbprint)
        {
            DisconnectFromAllListeners(clientThumbprint);
            trustProvider.Remove(clientThumbprint);
        }

        public void TrustOnly(IReadOnlyList<string> thumbprints)
        {
            var thumbprintsRevoked = trustProvider.ToArray().Except(thumbprints).ToArray();

            trustProvider.TrustOnly(thumbprints);

            DisconnectFromAllListeners(thumbprintsRevoked);
        }

        void DisconnectFromAllListeners(IReadOnlyCollection<string> thumbprints)
        {
            foreach (var thumbprint in thumbprints)
            {
                DisconnectFromAllListeners(thumbprint);
            }
        }

        void DisconnectFromAllListeners(string thumbprint)
        {
            foreach (var secureListener in listeners.OfType<SecureListener>())
            {
                secureListener.Disconnect(thumbprint);
            }
        }

        public bool IsTrusted(string remoteThumbprint)
        {
            return trustProvider.IsTrusted(remoteThumbprint);
        }

        public void Route(ServiceEndPoint to, ServiceEndPoint via)
        {
            routeTable.TryAdd(to.BaseUri, via);
        }

        public void SetFriendlyHtmlPageContent(string html)
        {
            friendlyHtmlPageContent = html ?? DefaultFriendlyHtmlPageContent;
        }

        public void SetFriendlyHtmlPageHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            friendlyHtmlPageHeaders = headers?.ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, string>();
        }

        public void Disconnect(ServiceEndPoint endpoint)
        {
            var log = logs.ForEndpoint(endpoint.BaseUri);
            connectionManager.Disconnect(endpoint, log);
        }

        public void Dispose()
        {
            pollingClients.Dispose();
            connectionManager.Dispose();
            foreach (var listener in listeners)
            {
                listener.Dispose();
            }
        }

        protected UnauthorizedClientConnectResponse HandleUnauthorizedClientConnect(string clientName, string thumbPrint)
        {
            var result = OnUnauthorizedClientConnect?.Invoke(clientName, thumbPrint) ?? UnauthorizedClientConnectResponse.BlockConnection;
            if (result == UnauthorizedClientConnectResponse.TrustAndAllowConnection)
            {
                Trust(thumbPrint);
            }
            return result;
        }

#pragma warning disable DE0009 // API is deprecated
        public static bool OSSupportsWebSockets => Environment.OSVersion.Platform == PlatformID.Win32NT &&
                                                    Environment.OSVersion.Version >= new Version(6, 2);
#pragma warning restore DE0009 // API is deprecated
    }
}
