using System;
using System.Runtime.InteropServices;
using System.Threading;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class ActiveRemoteServiceAgent : IRemoteServiceAgent
    {
        readonly Uri subscription;
        readonly SecureClient secureClient;
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly PollingWindow pollingWindow = new PollingWindow();
        DateTimeOffset nextPollDue = DateTimeOffset.UtcNow;
        int working;

        public ActiveRemoteServiceAgent(Uri subscription, SecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
        }

        public bool ProcessNext()
        {
            var now = DateTimeOffset.UtcNow;
            if (now > nextPollDue)
            {
                var value = Interlocked.CompareExchange(ref working, 1, 0);
                if (value == 0)
                {
                    ThreadPool.QueueUserWorkItem(AgentThreadExecutor);
                    return true;
                }
            }

            return false;
        }

        private void AgentThreadExecutor(object ignored)
        {
            try
            {
                int exchanged = 0;
                secureClient.ExecuteTransaction(protocol =>
                {
                    exchanged = protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest);
                });

                if (exchanged > 0)
                {
                    pollingWindow.Reset();
                }

                nextPollDue = DateTimeOffset.UtcNow.Add(pollingWindow.Increment());
            }
            finally
            {
                working = 0;
            }
        }
    }
}