using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.Client;
using Halibut.Protocol;
using Halibut.Server.Dispatch;
using Halibut.Server.ServiceModel;
using Halibut.Services;

namespace Halibut.Server
{
    public class HalibutRuntime : IDisposable, IHalibutClient, IMessageExchangeParticipant
    {
        readonly ConcurrentDictionary<Uri, PendingRequestQueue> queues = new ConcurrentDictionary<Uri, PendingRequestQueue>();
        readonly List<IRemoteServiceAgent> remotePollingWorkers = new List<IRemoteServiceAgent>();
        readonly object sync = new object();
        readonly X509Certificate2 serverCertficiate;
        readonly List<SecureListener> listeners = new List<SecureListener>();
        readonly ConcurrentDictionary<Uri, ServiceEndPoint> routeTable = new ConcurrentDictionary<Uri, ServiceEndPoint>();
        readonly ServiceInvoker invoker;
        bool running;

        public HalibutRuntime(IServiceFactory serviceFactory, X509Certificate2 serverCertficiate)
        {
            this.serverCertficiate = serverCertficiate;
            invoker = new ServiceInvoker(serviceFactory);
            Start();
        }

        void Start()
        {
            running = true;
            var worker = new Thread(DispatchThread);
            worker.Name = "Halibut runtime dispatch thread";
            worker.Start();
        }

        PendingRequestQueue GetQueue(Uri target)
        {
            PendingRequestQueue queue;
            queues.TryGetValue(target, out queue);
            return queue;
        }

        void DispatchThread()
        {
            while (running)
            {
                var workDone = false;

                IRemoteServiceAgent[] workers;
                lock (sync)
                {
                    workers = remotePollingWorkers.ToArray();
                }

                foreach (var worker in workers)
                {
                    workDone |= worker.ProcessNext();
                }

                if (!workDone)
                {
                    Thread.Sleep(10);
                }
            }
        }

        public int Listen()
        {
            return Listen(0);
        }

        public int Listen(int port)
        {
            return Listen(new IPEndPoint(IPAddress.Any, port));
        }

        public int Listen(IPEndPoint endpoint)
        {
            var listener = new SecureListener(endpoint, serverCertficiate, this, HandleIncomingRequest);
            listeners.Add(listener);
            return listener.Start();
        }

        public void Subscription(ServiceEndPoint endPoint)
        {
            queues.AddOrUpdate(endPoint.BaseUri, u => new PendingRequestQueue(), (u, q) => q);
        }

        public void Poll(Uri subscription, ServiceEndPoint endPoint)
        {
            var queue = queues.AddOrUpdate(endPoint.BaseUri, u => new PendingRequestQueue(), (u, q) => q);
            var client = new SecureClient(subscription, endPoint, queue, serverCertficiate, HandleIncomingRequest);
            remotePollingWorkers.Add(new ActiveRemoteServiceAgent(client));
        }

        public TService CreateClient<TService>(string endpointBaseUri, string publicThumbprint)
        {
            return CreateClient<TService>(new ServiceEndPoint(endpointBaseUri, publicThumbprint));
        }

        public TService CreateClient<TService>(ServiceEndPoint endpoint)
        {
            return (TService)new HalibutProxy(SendOutgoingRequest, typeof(TService), endpoint).GetTransparentProxy();
        }

        ResponseMessage SendOutgoingRequest(RequestMessage request)
        {
            var endPoint = request.Destination;

            ServiceEndPoint routerEndPoint;
            if (routeTable.TryGetValue(endPoint.BaseUri, out routerEndPoint))
            {
                endPoint = routerEndPoint;
                request = new RequestMessage {ActivityId = request.ActivityId, Id = request.Id, Params = new[] {request}, Destination = endPoint, ServiceName = "Router", MethodName = "Route"};
            }

            var queue = GetQueue(endPoint.BaseUri) ?? CreateQueue(endPoint);
            if (queue != null) return queue.QueueAndWait(request);
            throw new Exception(string.Format("Message exchange cannot route the request '{0}' because it was destined for an unknown endpoint '{1}'", request, endPoint));
        }

        ResponseMessage HandleIncomingRequest(RequestMessage request)
        {
            // Is this message intended for /route? If so, unwrap the original message. 
            // If we have a route table entry for the original, then route it using SendOutgoingRequest again. Otherwise, 
            // pass it to the invoker since it must be intended for us.
            if (request.ServiceName == "Router")
            {
                var original = (RequestMessage) request.Params[0];

                ServiceEndPoint route;
                if (routeTable.TryGetValue(original.Destination.BaseUri, out route))
                {
                    // Needs to be routed again
                    return SendOutgoingRequest(original);
                }

                request = original;
            }

            return invoker.Invoke(request);
        }

        PendingRequestQueue CreateQueue(ServiceEndPoint endPoint)
        {
            switch (endPoint.BaseUri.Scheme.ToLowerInvariant())
            {
                case "https":
                    return CreateActiveWorkerQueue(endPoint);
                case "poll":
                    return queues.AddOrUpdate(endPoint.BaseUri, u => new PendingRequestQueue(), (u, q) => q);
                default: throw new ArgumentException("Unknown endpoint type: " + endPoint.BaseUri.Scheme);
            }
        }

        PendingRequestQueue CreateActiveWorkerQueue(ServiceEndPoint endPoint)
        {
            var queue = queues.AddOrUpdate(endPoint.BaseUri, u => new PendingRequestQueue(), (u, q) => q);
            var client = new SecureClient(null, endPoint, queue, serverCertficiate, HandleIncomingRequest);
            var worker = new ActiveRemoteServiceAgent(client);
            lock (sync)
            {
                remotePollingWorkers.Add(worker);
            }

            return queue;
        }

        IPendingRequestQueue IMessageExchangeParticipant.SelectQueue(IdentificationMessage clientIdentification)
        {
            return clientIdentification.Subscription == null 
                ? new PendingRequestQueue() 
                : GetQueue(clientIdentification.Subscription);
        }

        public void Trust(string evePublicThumbprint)
        {
        }

        public void Route(ServiceEndPoint to, ServiceEndPoint via)
        {
            routeTable.TryAdd(to.BaseUri, via);
        }

        public void Dispose()
        {
            foreach (var listener in listeners)
            {
                listener.Dispose();
            }
            running = false;
        }
    }
}