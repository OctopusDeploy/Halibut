using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueueAsync : IPendingRequestQueue, IAsyncDisposable
    {
        readonly List<RedisPendingRequest> queue = new();
        readonly Dictionary<string, RedisPendingRequest> inProgress = new();
        readonly SemaphoreSlim queueLock = new(1, 1);
        readonly AsyncManualResetEvent itemAddedToQueue = new(false);
        readonly ILog log;
        readonly TimeSpan pollingQueueWaitTimeout;
        readonly CancellationTokenSource entireQueueCancellationTokenSource = new();

        public PendingRequestQueueAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log) : this(
            log, 
            halibutTimeoutsAndLimits.PollingQueueWaitTimeout)
        {
            this.log = log;
        }

        public PendingRequestQueueAsync(ILog log, TimeSpan pollingQueueWaitTimeout)
        {
            this.log = log;
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            //cancellationToken = CancellationToken.None;
            
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.entireQueueCancellationTokenSource.Token);
            cancellationToken = cancellationTokenSource.Token;
            
            using var pending = new RedisPendingRequest(request, log);

            try
            {
                using (await queueLock.LockAsync(cancellationToken))
                {
                    queue.Add(pending);
                    inProgress.Add(request.Id, pending);
                    itemAddedToQueue.Set();
                }
            }
            catch (OperationCanceledException ex)
            {
                throw new ConnectingRequestCancelledException(ex);
            }

            try
            {
                await pending.WaitUntilComplete(() => Task.CompletedTask, () => null, cancellationToken);
            }
            finally
            {
                using (await queueLock.LockAsync(CancellationToken.None))
                {
                    queue.Remove(pending);
                    inProgress.Remove(request.Id);
                }
            }

            return pending.Response;
        }

        public bool IsEmpty
        {
            get
            {
                using (queueLock.Lock(CancellationToken.None))
                {
                    return queue.Count == 0;
                }

            }
        }

        public int Count
        {
            get
            {
                using (queueLock.Lock(CancellationToken.None))
                {
                    return queue.Count;
                }
            }
        }

        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();

            while (true)
            {
                var timeout = pollingQueueWaitTimeout - timer.Elapsed;
                var pending = await DequeueNextAsync(timeout, cancellationToken);
                if (pending == null)
                {
                    return null;
                }

                var result = await pending.RequestHasBeenCollectedAndWillBeTransferred();
                if (result)
                {
                    return new (pending.Request, pending.PendingRequestCancellationToken);
                }
            }
        }

        async Task<RedisPendingRequest?> DequeueNextAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var first = await TakeFirst(cancellationToken);
            if (first != null || timeout <= TimeSpan.Zero)
            {
                return first;
            }

            using var cleanupCancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cleanupCancellationTokenSource.Token);

            await Task.WhenAny(
                // ReSharper disable once MethodSupportsCancellation
                itemAddedToQueue.WaitAsync( /*Do not pass a cancellation token as it will increase memory usage and may cause memory leaks*/),
                Task.Delay(timeout, linkedCancellationTokenSource.Token));

            cleanupCancellationTokenSource.Cancel();

            itemAddedToQueue.Reset();

            return await TakeFirst(cancellationToken);
        }

        async Task<RedisPendingRequest?> TakeFirst(CancellationToken cancellationToken)
        {
            using (await queueLock.LockAsync(cancellationToken))
            {
                if (queue.Count == 0)
                {
                    return null;
                }

                var first = queue[0];
                queue.RemoveAt(0);

                if (queue.Count == 0)
                {
                    itemAddedToQueue.Reset();
                }

                return first;
            }
        }

        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            if (response == null)
            {
                return;
            }

            using (await queueLock.LockAsync(CancellationToken.None))
            {
                if (inProgress.TryGetValue(response.Id, out var pending))
                {
                    pending.SetResponse(response);
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            entireQueueCancellationTokenSource.Cancel();
            entireQueueCancellationTokenSource.Dispose();
            return new ValueTask();
        }
    }
}