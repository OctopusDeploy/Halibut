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
        bool? relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;

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

        public PendingRequestQueueBuilder WithRelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout(bool relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout)
        {
            this.relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout = relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;
            return this;
        }

        public IPendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            halibutTimeoutsAndLimits.PollingQueueWaitTimeout = pollingQueueWaitTimeout ?? halibutTimeoutsAndLimits.PollingQueueWaitTimeout;
            halibutTimeoutsAndLimits.RelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout = relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout ?? halibutTimeoutsAndLimits.RelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;

            return new PendingRequestQueueAsync(log, halibutTimeoutsAndLimits);
        }
    }
}