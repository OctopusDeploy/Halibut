using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
// ReSharper disable once RedundantUsingDirective : Used in .CORE
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
        readonly ConcurrentDictionary<Uri, IPendingRequestQueue> queues = new ConcurrentDictionary<Uri, IPendingRequestQueue>();
        readonly IPendingRequestQueueFactory queueFactory;
        readonly X509Certificate2 serverCertificate;
        readonly List<IDisposable> listeners = new List<IDisposable>();
        readonly ITrustProvider trustProvider;
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new ConcurrentDictionary<Uri, ServiceEndPoint>();
        readonly ServiceInvoker invoker;
        readonly ILogFactory logs;
        readonly ConnectionManager connectionManager = new ConnectionManager();
        readonly PollingClientCollection pollingClients = new PollingClientCollection();
        string friendlyHtmlPageContent = DefaultFriendlyHtmlPageContent;
        Dictionary<string, string> friendlyHtmlPageHeaders = new Dictionary<string, string>();
        readonly MessageSerializer messageSerializer = new MessageSerializer();

        [Obsolete]
        public HalibutRuntime(X509Certificate2 serverCertificate) : this(new NullServiceFactory(), serverCertificate, new DefaultTrustProvider())
        {
        }

        [Obsolete]
        public HalibutRuntime(X509Certificate2 serverCertificate, ITrustProvider trustProvider) : this(new NullServiceFactory(), serverCertificate, trustProvider)
        {
        }

        [Obsolete]
        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate) : this(serviceFactory, serverCertificate, new DefaultTrustProvider())
        {
        }

        [Obsolete]
        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate, ITrustProvider trustProvider)
        {
            // if you change anything here, also change the below internal ctor
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            messageSerializer.AddToMessageContract(serviceFactory.RegisteredServiceTypes.ToArray());
            invoker = new ServiceInvoker(serviceFactory);
            
            // these two are the reason we can't just call our internal ctor.
            logs = new LogFactory();
            queueFactory = new DefaultPendingRequestQueueFactory(logs);
        }
        
        internal HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate, ITrustProvider trustProvider, IPendingRequestQueueFactory queueFactory, ILogFactory logFactory)
        {
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            messageSerializer.AddToMessageContract(serviceFactory.RegisteredServiceTypes.ToArray());
            invoker = new ServiceInvoker(serviceFactory);
            
            logs = logFactory;
            this.queueFactory = queueFactory;
        }

        public ILogFactory Logs => logs;

        public Func<string, string, UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
        
        IPendingRequestQueue GetQueue(Uri target)
        {
            return queues.GetOrAdd(target, u => queueFactory.CreateQueue(target));
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
        
        ExchangeProtocolBuilder ExchangeProtocolBuilder()
        {
            return (stream, log) => new MessageExchangeProtocol(new MessageExchangeStream(stream, messageSerializer, log), log);
        }

        public int Listen(IPEndPoint endpoint)
        {
            var listener = new SecureListener(endpoint, serverCertificate, ExchangeProtocolBuilder(), HandleMessage, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect);
            lock (listeners)
            {
                listeners.Add(listener);
            }

            return listener.Start();
        }

        public void ListenWebSocket(string endpoint)
        {
            var listener = new SecureWebSocketListener(endpoint, serverCertificate, ExchangeProtocolBuilder(), HandleMessage, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect);
            lock (listeners)
            {
                listeners.Add(listener);
            }

            listener.Start();
        }

        Task HandleMessage(MessageExchangeProtocol protocol)
        {
            return protocol.ExchangeAsServerAsync(
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
                client = new SecureWebSocketClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, log, connectionManager);
#else
                throw new NotSupportedException("The netstandard build of this library cannot act as the client in a WebSocket polling setup");
#endif
            }
            else
            {
                client = new SecureClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, log, connectionManager);
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
            messageSerializer.AddToMessageContract(typeof(TService));
            
#if HAS_REAL_PROXY
#pragma warning disable 618
            return (TService)new HalibutProxy(SendOutgoingRequest, typeof(TService), endpoint, cancellationToken).GetTransparentProxy();
#pragma warning restore 618
#else
            var proxy = DispatchProxy.Create<TService, HalibutProxy>();
#pragma warning disable 618
            (proxy as HalibutProxy).Configure(SendOutgoingRequest, typeof(TService), endpoint, cancellationToken);
#pragma warning restore 618
            return proxy;
#endif
        }
        
        // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#warning-sync-over-async
        [Obsolete("Consider implementing an async HalibutProxy instead")]
        ResponseMessage SendOutgoingRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            return SendOutgoingRequestAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        async Task<ResponseMessage> SendOutgoingRequestAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var endPoint = request.Destination;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    return SendOutgoingHttpsRequest(request, cancellationToken);
                case "poll":
                    return await SendOutgoingPollingRequest(request, cancellationToken);
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }
        }

        ResponseMessage SendOutgoingHttpsRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var client = new SecureListeningClient(ExchangeProtocolBuilder(), request.Destination, serverCertificate, logs.ForEndpoint(request.Destination.BaseUri), connectionManager);

            ResponseMessage response = null;
            client.ExecuteTransaction(protocol =>
            {
                response = protocol.ExchangeAsClient(request);
            }, cancellationToken);
            return response;
        }

        async Task<ResponseMessage> SendOutgoingPollingRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return await queue.QueueAndWaitAsync(request, cancellationToken);
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
            lock (listeners)
            {
                foreach (var secureListener in listeners.OfType<SecureListener>())
                {
                    secureListener.Disconnect(thumbprint);
                }
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
            lock (listeners)
            {
                foreach (var listener in listeners)
                {
                    listener?.Dispose();
                }
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
        // ReSharper disable once InconsistentNaming
        public static bool OSSupportsWebSockets => Environment.OSVersion.Platform == PlatformID.Win32NT &&
                                                   Environment.OSVersion.Version >= new Version(6, 2);
#pragma warning restore DE0009 // API is deprecated
        
    }
}
