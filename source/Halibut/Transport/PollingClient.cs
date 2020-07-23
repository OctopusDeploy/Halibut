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

        public void Dispose()
        {
            working = false;
        }

        void ExecutePollingLoop(object ignored)
        {
            var retry = RetryPolicy.Create();
            while (working)
            {
                var sleepFor = TimeSpan.Zero;

                try
                {
                    try
                    {
                        retry.Try();
                        secureClient.ExecuteTransaction(protocol => { protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest); }, cancellationToken);
                        retry.Success();
                    }
                    finally
                    {
                        sleepFor = retry.GetSleepPeriod();
                    }
                }
                catch (HalibutClientException hce)
                {
                    log?.Write(EventType.Error, $"{hce.Message?.TrimEnd('.')}. Retrying in {sleepFor.TotalSeconds:n1} seconds");
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