using System;
using System.IO;
using Halibut.ServiceModel;
using Halibut.Diagnostics;
using Halibut.Tests.Support.TestAttributes;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;
        SyncOrAsync syncOrAsync;

        public PendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public PendingRequestQueueBuilder WithLog(ILog log)
        {
            this.log = log;
            return this;
        }

        public PendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public PendingRequestQueueBuilder WithSyncOrAsync(SyncOrAsync syncOrAsync)
        {
            this.syncOrAsync = syncOrAsync;
            return this;
        }
        
        public IPendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? HalibutLimits.PollingQueueWaitTimeout;
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            switch (syncOrAsync)
            {
                case SyncOrAsync.Async:
                    return new PendingRequestQueueAsync(log, pollingQueueWaitTimeout);
                case SyncOrAsync.Sync:
                    return new PendingRequestQueue(log, pollingQueueWaitTimeout);
                default:
                    throw new InvalidDataException($"Unknown {nameof(SyncOrAsync)} {syncOrAsync}");
            }
        }
    }
}