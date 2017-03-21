using System;
using System.Threading;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class PollingClient : IPollingClient
    {
        readonly Uri subscription;
        readonly ISecureClient secureClient;
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly Thread thread;
        bool working;

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
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
                catch (Exception)
                {
                    Thread.Sleep(5000);
                }
            }
        }

        public void Dispose()
        {
            working = false;
        }
    }
}