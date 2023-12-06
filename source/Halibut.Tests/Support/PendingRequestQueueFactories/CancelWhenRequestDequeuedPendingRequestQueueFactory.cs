using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support.PendingRequestQueueFactories
{
    /// <summary>
    /// CancelWhenRequestDequeuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
    /// </summary>
    public class CancelWhenRequestDequeuedPendingRequestQueueFactory : IPendingRequestQueueFactory
        {
            readonly CancellationTokenSource[] cancellationTokenSources;
            readonly IPendingRequestQueueFactory inner;

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource[] cancellationTokenSources)
            {
                this.cancellationTokenSources = cancellationTokenSources;
                this.inner = inner;
            }

            public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource): this(inner, new []{ cancellationTokenSource })
            {
            }

            public IPendingRequestQueue CreateQueue(Uri endpoint)
            {
                return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSources);
            }

            class Decorator : IPendingRequestQueue
            {
                readonly CancellationTokenSource[] cancellationTokenSources;
                readonly IPendingRequestQueue inner;

                public Decorator(IPendingRequestQueue inner, CancellationTokenSource[] cancellationTokenSources)
                {
                    this.inner = inner;
                    this.cancellationTokenSources = cancellationTokenSources;
                }

                public bool IsEmpty => inner.IsEmpty;
                public int Count => inner.Count;
                public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
                
                public async Task<RequestMessage?> DequeueAsync(CancellationToken cancellationToken)
                {
                    var response = await inner.DequeueAsync(cancellationToken);
                    
                    Parallel.ForEach(cancellationTokenSources, cancellationTokenSource => cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2)));

                    return response;
                }

                public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
                    => await inner.QueueAndWaitAsync(request, requestCancellationTokens);
            }
        }
}
