using System;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class PollingClient : IPollingClient
    {
        readonly Uri subscription;
        readonly ISecureClient secureClient;
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly ILog log;
        readonly Thread thread;
        bool working;

        [Obsolete("Use the overload that provides a logger. This remains for backwards compatibility.")]
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest)
            : this(subscription, secureClient, handleIncomingRequest, null)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            thread = new Thread(ExecutePollingLoop);
            thread.Name = "Polling client for " + secureClient.ServiceEndpoint + " for subscription " + subscription;
            thread.IsBackground = true;
        }

        public void Start()
        {
            working = true;
            thread.Start();
        }

        private void ExecutePollingLoop(object ignored)
        {
            while (working)
            {
                try
                {
                    secureClient.ExecuteTransaction(protocol =>
                    {
                        protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest);
                    });
                }
                catch (Exception ex)
                {
                    log?.WriteException(EventType.Error, "Exception in the polling loop, sleeping for 5 seconds. This may be cause by a network error and usually rectifies itself. Disregard this message unless you are having communication problems.", ex);
                    Thread.Sleep(5000);
                }
            }
        }

        public void Dispose()
        {
            working = false;
            secureClient.Dispose();
        }
    }
}