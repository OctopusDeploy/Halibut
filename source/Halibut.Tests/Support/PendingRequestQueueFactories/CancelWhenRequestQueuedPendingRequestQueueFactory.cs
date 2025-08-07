using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support.PendingRequestQueueFactories
{
    /// <summary>
    /// CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
    /// </summary>
    public class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        readonly CancellationTokenSource[] cancellationTokenSources;
        readonly IPendingRequestQueueFactory inner;

        public CancelWhenRequestQueuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource[] cancellationTokenSources)
        {
            this.cancellationTokenSources = cancellationTokenSources;
            this.inner = inner;
        }

        public CancelWhenRequestQueuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource) : this(inner, new[]{ cancellationTokenSource }) {
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
            public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId) => await inner.ApplyResponse(response, requestActivityId);
            public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken) => await inner.DequeueAsync(cancellationToken);

            public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationTokens)
            {
                var task = Task.Run(async () =>
                    {
                        while (inner.IsEmpty)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
                        }

                        Parallel.ForEach(cancellationTokenSources, cancellationTokenSource => cancellationTokenSource.Cancel());
                    },
                    CancellationToken.None);

                var result = await inner.QueueAndWaitAsync(request, cancellationTokens);
                await task;
                return result;
            }

            public ValueTask DisposeAsync()
            {
                return this.inner.DisposeAsync();
            }
        }
    }
}
