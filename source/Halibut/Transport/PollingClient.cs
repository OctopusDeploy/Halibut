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
        readonly Func<RequestMessage, ResponseMessage> handleIncomingRequest;
        readonly ILog log;
        readonly ISecureClient secureClient;
        readonly Uri subscription;
        readonly Thread thread;
        readonly CancellationToken cancellationToken;
        bool working;
        Func<RetryPolicy> CreateRetryPolicy;

        [Obsolete("Use the overload that provides a logger. This remains for backwards compatibility.")]
        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, Func<RetryPolicy> createRetryPolicy)
            : this(subscription, secureClient, handleIncomingRequest, null, createRetryPolicy)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log, Func<RetryPolicy> createRetryPolicy)
            : this(subscription, secureClient, handleIncomingRequest, log, CancellationToken.None, createRetryPolicy)
        {
        }

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, ResponseMessage> handleIncomingRequest, ILog log, CancellationToken cancellationToken, Func<RetryPolicy> createRetryPolicy)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
            this.log = log;
            this.cancellationToken = cancellationToken;
            CreateRetryPolicy = createRetryPolicy;
            thread = new Thread(ExecutePollingLoop);
            thread.Name = "Polling client for " + secureClient.ServiceEndpoint + " for subscription " + subscription;
            thread.IsBackground = true;
        }

        public void Start()
        {
            working = true;
            thread.Start();
        }

        public void Dispose()
        {
            working = false;
        }

        void ExecutePollingLoop(object ignored)
        {
            var retry = CreateRetryPolicy();
            var sleepFor = TimeSpan.Zero;
            while (working)
            {
                try
                {
                    try
                    {
                        retry.Try();
                        secureClient.ExecuteTransaction(protocol =>
                        {
                            // We have successfully connected at this point so reset the retry policy
                            // Subsequent connection issues will try and reconnect quickly and then back-off
                            retry.Success();

                            protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest);
                        }, cancellationToken);
                        retry.Success();
                    }
                    finally
                    {
                        sleepFor = retry.GetSleepPeriod();
                    }
                }
                catch (HalibutClientException ex)
                {
                    log?.WriteException(EventType.Error, $"Halibut client exception: {ex.Message?.TrimEnd('.')}. Retrying in {sleepFor.TotalSeconds:n1} seconds", ex);
                }
                catch (Exception ex)
                {
                    log?.WriteException(EventType.Error, $"Exception in the polling loop. Retrying in {sleepFor.TotalSeconds:n1} seconds. This may be cause by a network error and usually rectifies itself. Disregard this message unless you are having communication problems.", ex);
                }
                finally
                {
                    Thread.Sleep(sleepFor);
                }
            }
        }
    }
}