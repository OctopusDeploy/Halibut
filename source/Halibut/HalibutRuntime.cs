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
using Halibut.Transport.Caching;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut
{
    public class HalibutRuntime : IHalibutRuntime
    {
        public static readonly string DefaultFriendlyHtmlPageContent = "<html><body><p>Hello!</p></body></html>";
        readonly ConcurrentDictionary<Uri, IPendingRequestQueue> queues = new();
        readonly IPendingRequestQueueFactory queueFactory;
        readonly X509Certificate2 serverCertificate;
        readonly List<IDisposable> listeners = new();
        readonly ITrustProvider trustProvider;
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new();
        readonly IServiceInvoker invoker;
        readonly ILogFactory logs;
        readonly IConnectionManager connectionManager;
        readonly PollingClientCollection pollingClients = new();
        string friendlyHtmlPageContent = DefaultFriendlyHtmlPageContent;
        Dictionary<string, string> friendlyHtmlPageHeaders = new();
        readonly IMessageSerializer messageSerializer;
        readonly ITypeRegistry typeRegistry;
        readonly Lazy<ResponseCache> responseCache = new();
        readonly Func<RetryPolicy> pollingReconnectRetryPolicy;
        public HalibutTimeoutsAndLimits TimeoutsAndLimits { get; }

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
            AsyncHalibutFeature = AsyncHalibutFeature.Disabled;
            this.TimeoutsAndLimits = null;
            // if you change anything here, also change the below internal ctor
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            pollingReconnectRetryPolicy = RetryPolicy.Create;

            // these two are the reason we can't just call our internal ctor.
            logs = new LogFactory();
            queueFactory = new DefaultPendingRequestQueueFactory(logs);
            typeRegistry = new TypeRegistry();
            typeRegistry.AddToMessageContract(serviceFactory.RegisteredServiceTypes.ToArray());
            messageSerializer = new MessageSerializerBuilder(logs)
                .WithTypeRegistry(typeRegistry)
                .Build();
            invoker = new ServiceInvoker(serviceFactory);

            connectionManager = new ConnectionManager();
        }

        internal HalibutRuntime(
            IServiceFactory serviceFactory, 
            X509Certificate2 serverCertificate, 
            ITrustProvider trustProvider, 
            IPendingRequestQueueFactory queueFactory, 
            ILogFactory logFactory, 
            ITypeRegistry typeRegistry, 
            IMessageSerializer messageSerializer, 
            Func<RetryPolicy> pollingReconnectRetryPolicy,
            AsyncHalibutFeature asyncHalibutFeature,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            AsyncHalibutFeature = asyncHalibutFeature;
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            logs = logFactory;
            this.queueFactory = queueFactory;
            this.typeRegistry = typeRegistry;
            this.messageSerializer = messageSerializer;
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            invoker = new ServiceInvoker(serviceFactory);
            TimeoutsAndLimits = halibutTimeoutsAndLimits;

            if (asyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                connectionManager = new ConnectionManagerAsync();
            }
            else
            {
                if (halibutTimeoutsAndLimits != null)
                {
                    throw new Exception($"{nameof(halibutTimeoutsAndLimits)} must be null when in sync mode");
                }
#pragma warning disable CS0612
                connectionManager = new ConnectionManager();
#pragma warning restore CS0612
            }
        }

        public ILogFactory Logs => logs;

        public Func<string, string, UnauthorizedClientConnectResponse> OnUnauthorizedClientConnect { get; set; }
        public OverrideErrorResponseMessageCachingAction OverrideErrorResponseMessageCaching { get; set; }
        public AsyncHalibutFeature AsyncHalibutFeature { get; }

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
            return (stream, log) => new MessageExchangeProtocol(new MessageExchangeStream(stream, messageSerializer, AsyncHalibutFeature, TimeoutsAndLimits, log), log);
        }

        public int Listen(IPEndPoint endpoint)
        {
            ExchangeActionAsync exchangeActionAsync = AsyncHalibutFeature.IsDisabled() ? HandleMessage : HandleMessageAsync;
            var listener = new SecureListener(endpoint, serverCertificate, ExchangeProtocolBuilder(), exchangeActionAsync, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect, AsyncHalibutFeature, TimeoutsAndLimits);
            lock (listeners)
            {
                listeners.Add(listener);
            }

            return listener.Start();
        }

        public void ListenWebSocket(string endpoint)
        {
            ExchangeActionAsync exchangeActionAsync = AsyncHalibutFeature.IsDisabled() ? HandleMessage : HandleMessageAsync;
            var listener = new SecureWebSocketListener(endpoint, serverCertificate, ExchangeProtocolBuilder(), exchangeActionAsync, IsTrusted, logs, () => friendlyHtmlPageContent, () => friendlyHtmlPageHeaders, HandleUnauthorizedClientConnect, AsyncHalibutFeature, TimeoutsAndLimits);
            
            lock (listeners)
            {
                listeners.Add(listener);
            }

            listener.Start();
        }

        Task HandleMessage(MessageExchangeProtocol protocol, CancellationToken cancellationToken)
        {
#pragma warning disable CS0612
            return protocol.ExchangeAsServerSynchronouslyAsync(
                HandleIncomingRequest,
                id => GetQueue(id.SubscriptionId));
#pragma warning restore CS0612
        }

        Task HandleMessageAsync(MessageExchangeProtocol protocol, CancellationToken cancellationToken)
        {
            return protocol.ExchangeAsServerAsync(HandleIncomingRequest, id => GetQueue(id.SubscriptionId), cancellationToken);
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
                client = new SecureWebSocketClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, AsyncHalibutFeature, TimeoutsAndLimits, log, connectionManager);
#else
                throw new NotSupportedException("The netstandard build of this library cannot act as the client in a WebSocket polling setup");
#endif
            }
            else
            {
                client = new SecureClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, AsyncHalibutFeature, TimeoutsAndLimits, log, connectionManager);
            }
            pollingClients.Add(new PollingClient(subscription, client, HandleIncomingRequest, log, cancellationToken, pollingReconnectRetryPolicy, AsyncHalibutFeature));
        }

        [Obsolete]
        public ServiceEndPoint Discover(Uri uri)
        {
            return Discover(uri, CancellationToken.None);
        }

        [Obsolete]
        public ServiceEndPoint Discover(Uri uri, CancellationToken cancellationToken)
        {
            return Discover(new ServiceEndPoint(uri, null), cancellationToken);
        }

        public async Task<ServiceEndPoint> DiscoverAsync(Uri uri, CancellationToken cancellationToken)
        {
            return await DiscoverAsync(new ServiceEndPoint(uri, null, TimeoutsAndLimits), cancellationToken);
        }

        [Obsolete]
        public ServiceEndPoint Discover(ServiceEndPoint endpoint)
        {
            return Discover(endpoint, CancellationToken.None);
        }

        [Obsolete]
        public ServiceEndPoint Discover(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var client = new DiscoveryClient();
            return client.Discover(endpoint, cancellationToken);
        }

        public async Task<ServiceEndPoint> DiscoverAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var client = new DiscoveryClient();
            return await client.DiscoverAsync(endpoint, TimeoutsAndLimits, cancellationToken);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint)
        {
            return CreateClient<TService>(new ServiceEndPoint(new Uri(endpointBaseUri), publicThumbprint, TimeoutsAndLimits), CancellationToken.None);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint, CancellationToken cancellationToken)
        {
            return CreateClient<TService>(new ServiceEndPoint(new Uri(endpointBaseUri), publicThumbprint, TimeoutsAndLimits), cancellationToken);
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint)
        {
            return CreateClient<TService>(endpoint, CancellationToken.None);
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            return CreateClient<TService, TService>(endpoint, cancellationToken);
        }

        public TClientService CreateClient<TService, TClientService>(ServiceEndPoint endpoint)
        {
            return CreateClient<TService, TClientService>(endpoint, CancellationToken.None);
        }

        private TClientService CreateClient<TService, TClientService>(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            typeRegistry.AddToMessageContract(typeof(TService));
            var logger = logs.ForEndpoint(endpoint.BaseUri);
#pragma warning disable CS0612
#if HAS_REAL_PROXY
#pragma warning disable 618
            return (TClientService)new HalibutProxy(SendOutgoingRequest, typeof(TService), typeof(TClientService), endpoint, logger, cancellationToken).GetTransparentProxy();
#pragma warning restore 618
#else
            var proxy = DispatchProxy.Create<TClientService, HalibutProxy>();
#pragma warning disable 618
            (proxy as HalibutProxy).Configure(SendOutgoingRequest, typeof(TService), endpoint, logger, cancellationToken);
#pragma warning restore 618
            return proxy;
#endif
#pragma warning restore CS0612
        }

        public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint endpoint)
        {
            if (AsyncHalibutFeature.IsDisabled())
            {
                throw new InvalidOperationException("Async client creation is not enabled. Please set AsyncHalibutFeature to Enabled to use async clients.");
            }

            typeRegistry.AddToMessageContract(typeof(TService));
            var logger = logs.ForEndpoint(endpoint.BaseUri);

            var proxy = DispatchProxyAsync.Create<TAsyncClientService, HalibutProxyWithAsync>();
            (proxy as HalibutProxyWithAsync)!.Configure(SendOutgoingRequestAsync, typeof(TService), endpoint, logger, CancellationToken.None);
            return proxy;
        }

        [Obsolete("Use SendOutgoingRequestAsync")]
        ResponseMessage SendOutgoingRequest(RequestMessage request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return SendOutgoingRequestSynchronouslyAsync(request, methodInfo, cancellationToken).GetAwaiter().GetResult();
        }

        [Obsolete]
        async Task<ResponseMessage> SendOutgoingRequestSynchronouslyAsync(RequestMessage request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var endPoint = request.Destination;

            var cachedResponse = responseCache.Value.GetCachedResponse(endPoint, request, methodInfo);

            if (cachedResponse != null)
            {
                return cachedResponse;
            }

            ResponseMessage response;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    // ReSharper disable once MethodHasAsyncOverload
                    response = SendOutgoingHttpsRequest(request, cancellationToken);
                    break;
                case "poll":
                    response = await SendOutgoingPollingRequest(request, cancellationToken);
                    break;
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }

            responseCache.Value.CacheResponse(endPoint, request, methodInfo, response, OverrideErrorResponseMessageCaching);

            return response;
        }

        async Task<ResponseMessage> SendOutgoingRequestAsync(RequestMessage request, MethodInfo methodInfo, RequestCancellationTokens requestCancellationTokens)
        {
            var endPoint = request.Destination;

            var cachedResponse = responseCache.Value.GetCachedResponse(endPoint, request, methodInfo);

            if (cachedResponse != null)
            {
                return cachedResponse;
            }

            ResponseMessage response;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    response = await SendOutgoingHttpsRequestAsync(request, requestCancellationTokens).ConfigureAwait(false);
                    break;
                case "poll":
                    response = await SendOutgoingPollingRequestAsync(request, requestCancellationTokens).ConfigureAwait(false);
                    break;
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }

            responseCache.Value.CacheResponse(endPoint, request, methodInfo, response, OverrideErrorResponseMessageCaching);

            return response;
        }

        [Obsolete]
        ResponseMessage SendOutgoingHttpsRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var client = new SecureListeningClient(ExchangeProtocolBuilder(), request.Destination, serverCertificate, AsyncHalibutFeature, TimeoutsAndLimits, logs.ForEndpoint(request.Destination.BaseUri), connectionManager);

            ResponseMessage response = null;
            client.ExecuteTransaction(protocol =>
            {
                response = protocol.ExchangeAsClient(request);
            }, cancellationToken);
            return response;
        }

        async Task<ResponseMessage> SendOutgoingHttpsRequestAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
        {
            var client = new SecureListeningClient(ExchangeProtocolBuilder(), request.Destination, serverCertificate, AsyncHalibutFeature, TimeoutsAndLimits, logs.ForEndpoint(request.Destination.BaseUri), connectionManager);

            ResponseMessage response = null;

            await client.ExecuteTransactionAsync(
                async (protocol, cts) =>
                {
                    response = await protocol.ExchangeAsClientAsync(request, cts).ConfigureAwait(false);
                }, 
                requestCancellationTokens).ConfigureAwait(false);

            return response;
        }

        async Task<ResponseMessage> SendOutgoingPollingRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return await queue.QueueAndWaitAsync(request, cancellationToken);
        }

        async Task<ResponseMessage> SendOutgoingPollingRequestAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return await queue.QueueAndWaitAsync(request, requestCancellationTokens);
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

        [Obsolete]
        public void Disconnect(ServiceEndPoint endpoint)
        {
            var log = logs.ForEndpoint(endpoint.BaseUri);
            connectionManager.Disconnect(endpoint, log);
        }

        public async Task DisconnectAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var log = logs.ForEndpoint(endpoint.BaseUri);
            await connectionManager.DisconnectAsync(endpoint, log, cancellationToken);
        }

        public void Dispose()
        {
            pollingClients.Dispose();
            connectionManager.Dispose();
            
            if (responseCache.IsValueCreated)
            {
                responseCache.Value?.Dispose();
            }

            lock (listeners)
            {
                foreach (var listener in listeners)
                {
                    listener?.Dispose();
                }
            }

            if (responseCache.IsValueCreated)
            {
                responseCache.Value.Dispose();
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
