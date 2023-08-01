using System;
using Halibut.ServiceModel;
using Halibut.Diagnostics;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder
    {
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;
        bool async;

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

        public PendingRequestQueueBuilder WithAsync(bool async)
        {
            this.async = async;
            return this;
        }

        public IPendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? HalibutLimits.PollingQueueWaitTimeout;

            var log = new InMemoryConnectionLog(endpoint);

            if (async)
            {
                return new PendingRequestQueueAsync(log, pollingQueueWaitTimeout);
            }

            return new PendingRequestQueue(log, pollingQueueWaitTimeout);
        }
    }
}