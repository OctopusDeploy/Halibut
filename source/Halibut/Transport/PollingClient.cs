using System;
using System.Threading;
using System.Threading.Tasks;
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
        }

        public void Start()
        {
            working = true;
            Task.Run(ExecutePollingLoop);
        }

        public void Dispose()
        {
            working = false;
        }

        async Task ExecutePollingLoop()
        {
            var retry = RetryPolicy.Create();
            var sleepFor = TimeSpan.Zero;
            while (working)
            {
                try
                {
                    try
                    {
                        retry.Try();
                        await secureClient.ExecuteTransaction(async protocol => { await protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest); }, cancellationToken);
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