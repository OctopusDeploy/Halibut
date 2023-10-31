using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

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
        
        public IPendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? new HalibutTimeoutsAndLimitsForTestsBuilder().Build().PollingQueueWaitTimeout;
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            return new PendingRequestQueueAsync(log, pollingQueueWaitTimeout);
        }
    }
}