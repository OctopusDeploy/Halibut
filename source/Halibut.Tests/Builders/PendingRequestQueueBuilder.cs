using System;
using Halibut.ServiceModel;
using Halibut.Diagnostics;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder
    {
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

        public PendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public PendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public PendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? HalibutLimits.PollingQueueWaitTimeout;

            var log = new InMemoryConnectionLog(endpoint);

            var pendingRequestQueue = new PendingRequestQueue(log, pollingQueueWaitTimeout);
            return pendingRequestQueue;
        }
    }
}