using System;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;

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
        CancellationToken cancellationToken;

        [Obsolete("Use the overload that provides a logger. This remains for backwards compatibility.")]
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest)
            : this(subscription, secureClient, handleIncomingRequest, null)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log)
        : this(subscription, secureClient, handleIncomingRequest, log, CancellationToken.None)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log, CancellationToken cancellationToken)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            this.cancellationToken = cancellationToken;
            thread = new Thread(ExecutePollingLoop);
            thread.Name = "Polling client for " + secureClient.ServiceEndpoint + " for subscription " + subscription;
            thread.IsBackground = true;
        }

        public void Start()
        {
            working = true;
            thread.Start();
        }

        void ExecutePollingLoop(object ignored)
        {
            var retry = RetryPolicy.Create();
            while (working)
            {
                try
                {
                    retry.Try();
                    secureClient.ExecuteTransaction(protocol =>
                    {
                        protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest);
                    }, cancellationToken);
                    retry.Success();
                }
                catch (HalibutClientException hce)
                {
                    var sleepFor = retry.GetSleepPeriod();
                    log?.Write(EventType.Error, $"{hce.Message?.TrimEnd('.')}. Retrying in {sleepFor.TotalSeconds:n1} seconds");
                    Thread.Sleep(sleepFor);
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
        }
    }
}