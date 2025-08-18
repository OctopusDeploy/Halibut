
using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using DisposableCollection = Halibut.Util.DisposableCollection;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder : IPendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

        public IPendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public IPendingRequestQueueBuilder WithLog(ILog log)
        {
            this.log = log;
            return this;
        }

        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public QueueHolder Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? halibutTimeoutsAndLimits.PollingQueueWaitTimeout;

            return new QueueHolder(new PendingRequestQueueAsync(log, pollingQueueWaitTimeout), new DisposableCollection());
        }
    }

    public class QueueHolder : IAsyncDisposable
    {
        public IPendingRequestQueue PendingRequestQueue { get; }
        public DisposableCollection DisposableCollection { get; }

        public QueueHolder(IPendingRequestQueue pendingRequestQueue, DisposableCollection disposableCollection)
        {
            this.PendingRequestQueue = pendingRequestQueue;
            this.DisposableCollection = disposableCollection;
        }

        public async ValueTask DisposeAsync()
        {
            this.DisposableCollection.AddAsyncDisposable(PendingRequestQueue);
            await DisposableCollection.DisposeAsync();
        }
    }
}