using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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
        X509Certificate2 serverCertificate;
        readonly List<Stoppable> listeners = new List<Stoppable>();
        readonly HashSet<string> trustedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new ConcurrentDictionary<Uri, ServiceEndPoint>();
        readonly ServiceInvoker invoker;
        readonly LogFactory logs = new LogFactory();
        ConnectionPool<ServiceEndPoint, IConnection> pool = new ConnectionPool<ServiceEndPoint, IConnection>();
        readonly PollingClientCollection pollingClients = new PollingClientCollection();
        string friendlyHtmlPageContent = DefaultFriendlyHtmlPageContent;
        bool stopCalled;

        public HalibutRuntime(X509Certificate2 serverCertificate) : this(new NullServiceFactory(), serverCertificate)
        {
        }

        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertificate)
        {
            this.serverCertificate = serverCertificate;
            invoker = new ServiceInvoker(serviceFactory);
        }

        public LogFactory Logs
        {
            get { return logs; }
        }

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
            return Listen(new IPEndPoint(IPAddress.IPv6Any, port));
        }

        public int Listen(IPEndPoint endpoint)
        {
            var listener = new SecureListener(endpoint, serverCertificate, ListenerHandler, IsTrusted, logs, () => friendlyHtmlPageContent);
            listeners.Add(listener);
            return listener.Start();
        }

#if HAS_WEB_SOCKET_LISTENER
        public void ListenWebSocket(string endpoint)
        {
            var listener = new SecureWebSocketListener(endpoint, serverCertificate, ListenerHandler, IsTrusted, logs, () => friendlyHtmlPageContent);
            listeners.Add(listener);
            listener.Start();
        }
#endif

        Task ListenerHandler(MessageExchangeProtocol obj)
        {
            return obj.ExchangeAsServer(
                HandleIncomingRequest,
                id => GetQueue(id.SubscriptionId));
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint)
        {
            ISecureClient client;
            if (endPoint.IsWebSocketEndpoint)
            {
#if HAS_SERVICE_POINT_MANAGER
                client = new SecureWebSocketClient(endPoint, serverCertificate, logs.ForEndpoint(endPoint.BaseUri), pool);
#else
                throw new NotImplementedException("Web Sockets are not available on this platform");
#endif
            }
            else
            {
                client = new SecureClient(endPoint, serverCertificate, logs.ForEndpoint(endPoint.BaseUri), pool);
            }
            pollingClients.Add(new PollingClient(subscription, client, HandleIncomingRequest));
        }

        public Task<ServiceEndPoint> Discover(Uri uri)
        {
            return Discover(new ServiceEndPoint(uri, null));
        }

        public Task<ServiceEndPoint> Discover(ServiceEndPoint endpoint)
        {
            var client = new DiscoveryClient();
            return client.Discover(endpoint);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprint));
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint)
        {
            var proxy = DispatchProxyAsync.Create<TService, HalibutProxy>();
            (proxy as HalibutProxy).Configure(SendOutgoingRequest, typeof(TService), endpoint);
            return proxy;
        }

        async Task<ResponseMessage> SendOutgoingRequest(RequestMessage request)
        {
            var endPoint = request.Destination;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    return await SendOutgoingHttpsRequest(request).ConfigureAwait(false);
                case "poll":
                    return await SendOutgoingPollingRequest(request).ConfigureAwait(false);
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }
        }

        async Task<ResponseMessage> SendOutgoingHttpsRequest(RequestMessage request)
        {
            var client = new SecureClient(request.Destination, serverCertificate, logs.ForEndpoint(request.Destination.BaseUri), pool);

            ResponseMessage response = null;
            await client.ExecuteTransaction(async protocol =>
            {
                response = await protocol.ExchangeAsClient(request).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return response;
        }

        Task<ResponseMessage> SendOutgoingPollingRequest(RequestMessage request)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return queue.QueueAndWait(request);
        }

        Task<ResponseMessage> HandleIncomingRequest(RequestMessage request)
        {
            return invoker.Invoke(request);
        }

        public void Trust(string clientThumbprint)
        {
            lock (trustedThumbprints)
                trustedThumbprints.Add(clientThumbprint);
        }

        public void RemoveTrust(string clientThumbprint)
        {
            lock (trustedThumbprints)
                trustedThumbprints.Remove(clientThumbprint);
        }


        public void TrustOnly(IReadOnlyList<string> thumbprints)
        {
            lock (trustedThumbprints)
            {
                trustedThumbprints.Clear();
                foreach(var thumbprint in thumbprints)
                    trustedThumbprints.Add(thumbprint);
            }
        }

        public bool IsTrusted(string remoteThumbprint)
        {
            lock (trustedThumbprints)
                return trustedThumbprints.Contains(remoteThumbprint);
        }

        public void Route(ServiceEndPoint to, ServiceEndPoint via)
        {
            routeTable.TryAdd(to.BaseUri, via);
        }

        public void SetFriendlyHtmlPageContent(string html)
        {
            friendlyHtmlPageContent = html ?? DefaultFriendlyHtmlPageContent;
        }

        public async Task Stop()
        {
            stopCalled = true;
            await pollingClients.Stop().ConfigureAwait(false);

            foreach (var listener in listeners)
            {
                await listener.Stop().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            // Injected by Fody.Janitor
        }

        public void DisposeManaged()
        {
            if (!stopCalled)
            {
                throw new Exception("Call Stop!");
            }
        }

#if HAS_WEB_SOCKET_LISTENER
        public static bool OSSupportsWebSockets => Environment.OSVersion.Version >= new Version(6, 2);
#endif
        }
}