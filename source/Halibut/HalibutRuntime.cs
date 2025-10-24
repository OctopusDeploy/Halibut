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
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut
{
    public class HalibutRuntime : IHalibutRuntime
    {
        public static readonly string DefaultFriendlyHtmlPageContent = "<html><body><p>Hello!</p></body></html>";
        readonly ConcurrentDictionary<Uri, IPendingRequestQueue> queues = new();
        readonly IPendingRequestQueueFactory queueFactory;
        readonly X509Certificate2 serverCertificate;
        readonly MutexedItem<List<IAsyncDisposable>> listeners = new MutexedItem<List<IAsyncDisposable>>(new List<IAsyncDisposable>());
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
        readonly IStreamFactory streamFactory;
        readonly IRpcObserver rpcObserver;
        readonly TcpConnectionFactory tcpConnectionFactory;
        readonly IConnectionsObserver connectionsObserver;
        readonly ISecureConnectionObserver secureConnectionObserver;
        readonly IActiveTcpConnectionsLimiter activeTcpConnectionsLimiter;
        readonly IControlMessageObserver controlMessageObserver;
        readonly ISslConfigurationProvider sslConfigurationProvider;

        internal HalibutRuntime(
            IServiceFactory serviceFactory,
            X509Certificate2 serverCertificate,
            ITrustProvider trustProvider,
            IPendingRequestQueueFactory queueFactory,
            ILogFactory logFactory,
            ITypeRegistry typeRegistry,
            IMessageSerializer messageSerializer,
            Func<RetryPolicy> pollingReconnectRetryPolicy,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            IStreamFactory streamFactory,
            IRpcObserver rpcObserver,
            IConnectionsObserver connectionsObserver, 
            IControlMessageObserver controlMessageObserver,
            ISecureConnectionObserver secureConnectionObserver,
            ISslConfigurationProvider sslConfigurationProvider
        )
        {
            this.serverCertificate = serverCertificate;
            this.trustProvider = trustProvider;
            logs = logFactory;
            this.queueFactory = queueFactory;
            this.typeRegistry = typeRegistry;
            this.messageSerializer = messageSerializer;
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            this.streamFactory = streamFactory;
            this.rpcObserver = rpcObserver;
            invoker = new ServiceInvoker(serviceFactory);
            TimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.connectionsObserver = connectionsObserver;
            this.secureConnectionObserver = secureConnectionObserver;
            this.controlMessageObserver = controlMessageObserver;
            this.sslConfigurationProvider = sslConfigurationProvider;

            connectionManager = new ConnectionManagerAsync();
            tcpConnectionFactory = new TcpConnectionFactory(serverCertificate, TimeoutsAndLimits, streamFactory, secureConnectionObserver, sslConfigurationProvider);
            activeTcpConnectionsLimiter = new ActiveTcpConnectionsLimiter(TimeoutsAndLimits);
        }

        public ILogFactory Logs => logs;

        public Func<string, string, UnauthorizedClientConnectResponse>? OnUnauthorizedClientConnect { get; set; }
        public OverrideErrorResponseMessageCachingAction? OverrideErrorResponseMessageCaching { get; set; }

        IPendingRequestQueue GetQueue(Uri target)
        {
            IPendingRequestQueue? createdQueue = null;
            var queue = queues.GetOrAdd(target, u => createdQueue = queueFactory.CreateQueue(target));
            if (createdQueue != null && !ReferenceEquals(createdQueue, queue))
            {
                // We created a queue that won't be used, dispose of it in the background.
                Task.Run(() => Try.IgnoringError(() => createdQueue.DisposeAsync()));
            }

            return queue;
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
            return (stream, log) => new MessageExchangeProtocol(new MessageExchangeStream(stream, messageSerializer, controlMessageObserver, TimeoutsAndLimits, log), TimeoutsAndLimits, activeTcpConnectionsLimiter, log);
        }

        public int Listen(IPEndPoint endpoint)
        {
            var listener = new SecureListener(endpoint,
                serverCertificate,
                ExchangeProtocolBuilder(),
                HandleMessageAsync,
                IsTrusted,
                logs,
                () => friendlyHtmlPageContent,
                () => friendlyHtmlPageHeaders,
                HandleUnauthorizedClientConnect,
                TimeoutsAndLimits,
                streamFactory,
                connectionsObserver,
                secureConnectionObserver,
                sslConfigurationProvider
            );

            listeners.DoWithExclusiveAccess(l =>
            {
                l.Add(listener);
            });

            return listener.Start();
        }

        public void ListenWebSocket(string endpoint)
        {
            var listener = new SecureWebSocketListener(endpoint,
                serverCertificate,
                ExchangeProtocolBuilder(),
                HandleMessageAsync,
                IsTrusted,
                logs,
                () => friendlyHtmlPageContent,
                () => friendlyHtmlPageHeaders,
                HandleUnauthorizedClientConnect,
                TimeoutsAndLimits,
                streamFactory,
                connectionsObserver);

            
            listeners.DoWithExclusiveAccess(l =>
            {
                l.Add(listener);
            });

            listener.Start();
        }

        Task HandleMessageAsync(MessageExchangeProtocol protocol, CancellationToken cancellationToken)
        {
            return protocol.ExchangeAsServerAsync(HandleIncomingRequestAsync, id => GetQueue(id.SubscriptionId), cancellationToken);
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint, CancellationToken cancellationToken)
        {
            ISecureClient client;
            var log = logs.ForEndpoint(endPoint.BaseUri);
            if (endPoint.IsWebSocketEndpoint)
            {
#if SUPPORTS_WEB_SOCKET_CLIENT
                client = new SecureWebSocketClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, TimeoutsAndLimits, log, connectionManager, streamFactory);
#else
                throw new NotSupportedException("The netstandard build of this library cannot act as the client in a WebSocket polling setup");
#endif
            }
            else
            {
                client = new SecureClient(ExchangeProtocolBuilder(), endPoint, serverCertificate, log, connectionManager, tcpConnectionFactory);
            }

            pollingClients.Add(new PollingClient(subscription, client, HandleIncomingRequestAsync, log, cancellationToken, pollingReconnectRetryPolicy));
        }

        public async Task<ServiceEndPoint> DiscoverAsync(Uri uri, CancellationToken cancellationToken)
        {
            return await DiscoverAsync(new ServiceEndPoint(uri, null, TimeoutsAndLimits), cancellationToken);
        }

        public async Task<ServiceEndPoint> DiscoverAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var client = new DiscoveryClient(streamFactory, sslConfigurationProvider);
            return await client.DiscoverAsync(endpoint, TimeoutsAndLimits, cancellationToken);
        }

        public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint endpoint)
        {
            typeRegistry.AddToMessageContract(typeof(TService));
            var logger = logs.ForEndpoint(endpoint.BaseUri);

            var proxy = DispatchProxyAsync.Create<TAsyncClientService, HalibutProxyWithAsync>();
            (proxy as HalibutProxyWithAsync)!.Configure(SendOutgoingRequestAsync, typeof(TService), endpoint, logger);
            return proxy;
        }

        async Task<ResponseMessage> SendOutgoingRequestAsync(RequestMessage request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var endPoint = request.Destination;

            var cachedResponse = responseCache.Value.GetCachedResponse(endPoint, request, methodInfo);

            if (cachedResponse != null)
            {
                return cachedResponse;
            }

            rpcObserver.StartCall(request);
            try
            {
                ResponseMessage response;

                switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
                {
                    case "https":
                        response = await SendOutgoingHttpsRequestAsync(request, cancellationToken).ConfigureAwait(false);
                        break;
                    case "poll":
                        response = await SendOutgoingPollingRequestAsync(request, cancellationToken).ConfigureAwait(false);
                        break;
                    default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
                }

                responseCache.Value.CacheResponse(endPoint, request, methodInfo, response, OverrideErrorResponseMessageCaching);

                return response;
            }
            finally
            {
                rpcObserver.StopCall(request);
            }
        }

        async Task<ResponseMessage> SendOutgoingHttpsRequestAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var client = new SecureListeningClient(ExchangeProtocolBuilder(), request.Destination, serverCertificate, logs.ForEndpoint(request.Destination.BaseUri), connectionManager, tcpConnectionFactory);

            ResponseMessage response = null!;

            await client.ExecuteTransactionAsync(
                async (protocol, cts) =>
                {
                    response = await protocol.ExchangeAsClientAsync(request, cts).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            return response;
        }

        async Task<ResponseMessage> SendOutgoingPollingRequestAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return await queue.QueueAndWaitAsync(request, cancellationToken);
        }

        async Task<ResponseMessage> HandleIncomingRequestAsync(RequestMessage request)
        {
            return await invoker.InvokeAsync(request);
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
            listeners.DoWithExclusiveAccess(l =>
            {
                foreach (var secureListener in l.OfType<SecureListener>())
                {
                    secureListener.Disconnect(thumbprint);
                }
            });
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

        public async Task DisconnectAsync(ServiceEndPoint endpoint, CancellationToken cancellationToken)
        {
            var log = logs.ForEndpoint(endpoint.BaseUri);
            await connectionManager.DisconnectAsync(endpoint, log, cancellationToken);
        }

        /// <summary>
        /// Dispose has been left in the API for backwards compatibility, but it is recommended to use DisposeAsync instead.
        /// At the time this change was made, Tentacle (Tentacle Version 7.1.0) still uses this method.
        /// Care must be taken to ensure all code using HalibutRuntime support and calls DisposeAsync.
        /// </summary>
        [Obsolete("Dispose has been left in the API for backwards compatibility, but it is recommended to use DisposeAsync instead.")]
        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var pendingRequestQueue in this.queues.Values)
            {
                try
                {
                    await pendingRequestQueue.DisposeAsync();
                }
                catch (Exception)
                {
                }
            }
            pollingClients.Dispose();
            await connectionManager.DisposeAsync();

            if (responseCache.IsValueCreated)
            {
                responseCache.Value?.Dispose();
            }

            await listeners.DoWithExclusiveAccess(async l =>
            {
                foreach (var listener in l)
                {
                    if (listener != null)
                    {
                        await listener.DisposeAsync();
                    }
                }
            });
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

