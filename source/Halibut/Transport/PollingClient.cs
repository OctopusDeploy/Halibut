using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class PollingClient : IPollingClient
    {
        readonly Uri subscription;
        readonly ISecureClient secureClient;
        readonly Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequest;
        CancellationTokenSource tokenSource;
        Task pollerTask;

        public PollingClient(Uri subscription, ISecureClient secureClient, Func<RequestMessage, Task<ResponseMessage>> handleIncomingRequest)
        {
            this.subscription = subscription;
            this.secureClient = secureClient;
            this.handleIncomingRequest = handleIncomingRequest;
        }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();

            var token = tokenSource.Token;

            pollerTask = Task.Run(() => ExecutePollingLoop(token), CancellationToken.None);
        }

        public Task Stop()
        {
            tokenSource.Cancel();

            return pollerTask;
        }

        async Task ExecutePollingLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await secureClient.ExecuteTransaction(protocol => protocol.ExchangeAsSubscriber(subscription, handleIncomingRequest)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    try
                    {
                        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                }
            }
        }

        public void Dispose()
        {
            // Injected by Fody.Janitor
        }
    }
}