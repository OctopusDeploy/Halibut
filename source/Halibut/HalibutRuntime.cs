using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut
{
    public class HalibutRuntime : IDisposable
    {
        public static readonly string DefaultFriendlyHtmlPageContent = "<html><body><p>Hello!</p></body></html>";

        readonly ConcurrentDictionary<Uri, PendingRequestQueue> queues = new ConcurrentDictionary<Uri, PendingRequestQueue>();
        readonly X509Certificate2 serverCertificate;
        readonly List<SecureListener> listeners = new List<SecureListener>();
        readonly HashSet<string> trustedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase); 
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new ConcurrentDictionary<Uri, ServiceEndPoint>();
        readonly ServiceInvoker invoker;
        readonly LogFactory logs = new LogFactory();
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool = new ConnectionPool<ServiceEndPoint, IConnection>();
        readonly PollingClientCollection pollingClients = new PollingClientCollection();
        string friendlyHtmlPageContent = DefaultFriendlyHtmlPageContent;

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
            var listener = new SecureListener(endpoint, serverCertificate, ListenerHandler, VerifyThumbprintOfIncomingClient, logs, () => friendlyHtmlPageContent);
            listeners.Add(listener);
            return listener.Start();
        }

        void ListenerHandler(MessageExchangeProtocol obj)
        {
            obj.ExchangeAsServer(
                HandleIncomingRequest,
                id => GetQueue(id.SubscriptionId));
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint)
        {
            var client = new SecureClient(endPoint, serverCertificate, logs.ForEndpoint(endPoint.BaseUri), pool);
            pollingClients.Add(new PollingClient(subscription, client, HandleIncomingRequest));
        }

        public ServiceEndPoint Discover(Uri uri)
        {
            var client = new DiscoveryClient();
            return client.Discover(uri);
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprint));
        }

        public TService CreateClient<TService>(string endpointBaseUri, IEnumerable<string> publicThumbprints)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprints));
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint)
        {
            return (TService)new HalibutProxy(SendOutgoingRequest, typeof(TService), endpoint).GetTransparentProxy();
        }

        ResponseMessage SendOutgoingRequest(RequestMessage request)
        {
            var endPoint = request.Destination;

            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    return SendOutgoingHttpsRequest(request);
                case "poll":
                    return SendOutgoingPollingRequest(request);
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }
        }

        ResponseMessage SendOutgoingHttpsRequest(RequestMessage request)
        {
            var client = new SecureClient(request.Destination, serverCertificate, logs.ForEndpoint(request.Destination.BaseUri), pool);

            ResponseMessage response = null;
            client.ExecuteTransaction(protocol =>
            {
                response = protocol.ExchangeAsClient(request);
            });
            return response;
        }

        ResponseMessage SendOutgoingPollingRequest(RequestMessage request)
        {
            var queue = GetQueue(request.Destination.BaseUri);
            return queue.QueueAndWait(request);
        }

        ResponseMessage HandleIncomingRequest(RequestMessage request)
        {
            return invoker.Invoke(request);
        }

        public void Trust(string clientThumbprint)
        {
            trustedThumbprints.Add(clientThumbprint);
        }

        bool VerifyThumbprintOfIncomingClient(string remoteThumbprint)
        {
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

        public void Dispose()
        {
            pollingClients.Dispose();
            pool.Dispose();
            foreach (var listener in listeners)
            {
                listener.Dispose();
            }
        }
    }
}