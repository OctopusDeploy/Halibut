using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support.PendingRequestQueueFactories
{
    /// <summary>
    /// CancelWhenRequestDequeuedPendingRequestQueueFactory cancels the cancellation token source when a request is dequeued
    /// </summary>
    class CancelWhenRequestDequeuedPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        readonly CancellationTokenSource cancellationTokenSource;
        readonly Func<bool>? shouldCancelOnDequeue;
        readonly Action<ResponseMessage>? onResponseApplied;
        readonly IPendingRequestQueueFactory inner;

        public CancelWhenRequestDequeuedPendingRequestQueueFactory(IPendingRequestQueueFactory inner, CancellationTokenSource cancellationTokenSource, Func<bool>? shouldCancelOnDequeue = null, Action<ResponseMessage>? onResponseApplied = null)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            this.shouldCancelOnDequeue = shouldCancelOnDequeue;
            this.inner = inner;
            this.onResponseApplied = onResponseApplied;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new Decorator(inner.CreateQueue(endpoint), cancellationTokenSource, shouldCancelOnDequeue, onResponseApplied);
        }

        public Task<IPendingRequestQueue> CreateQueueAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateQueue(endpoint));
        }

        class Decorator : IPendingRequestQueue
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly Func<bool>? shouldCancel;
            readonly Action<ResponseMessage>? onResponseApplied;
            readonly IPendingRequestQueue inner;

            public Decorator(IPendingRequestQueue inner, CancellationTokenSource cancellationTokenSource, Func<bool>? shouldCancel, Action<ResponseMessage>? onResponseApplied)
            {
                this.inner = inner;
                this.cancellationTokenSource = cancellationTokenSource;
                this.shouldCancel = shouldCancel;
                this.onResponseApplied = onResponseApplied;
            }

            public bool IsEmpty => inner.IsEmpty;
            public int Count => inner.Count;

            public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
            {
                onResponseApplied?.Invoke(response);
                await inner.ApplyResponse(response, requestActivityId);
            }

            public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
            {
                var response = await inner.DequeueAsync(cancellationToken);

                if (shouldCancel?.Invoke() ?? true)
                {
                    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                }

                return response;
            }

            public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
                => await inner.QueueAndWaitAsync(request, requestCancellationToken);

            public ValueTask DisposeAsync()
            {
                return inner.DisposeAsync();
            }
        }
    }
}