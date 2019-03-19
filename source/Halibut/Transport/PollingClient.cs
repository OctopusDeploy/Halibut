using System;
using System.Threading;
using System.Threading.Tasks;
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
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        Task backgroundTask;

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
        }

        public void Start()
        {
            backgroundTask = Task.Run(ExecutePollingLoop, CancellationToken.None);
        }

        async Task ExecutePollingLoop()
        {
            while (!cts.IsCancellationRequested)
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
                    await Task.Delay(5000, cts.Token).ContinueWith(_ => {}).ConfigureAwait(false); // Empty continuation so it does not throw TaskCanceledException
                }
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            backgroundTask?.Wait();
            backgroundTask?.Dispose();
            cts.Dispose();
        }
    }
}