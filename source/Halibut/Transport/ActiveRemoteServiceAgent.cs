using System;
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
                    nextPollDue = DateTimeOffset.UtcNow;
                }
                else
                {
                    nextPollDue = DateTimeOffset.UtcNow.AddSeconds(10);
                }
            }
            finally
            {
                working = 0;
            }
        }
    }
}