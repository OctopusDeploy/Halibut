using System;
using Halibut.ServiceModel;
using Halibut.Diagnostics;
using Halibut.Tests.Support;

namespace Halibut.Tests.Builders
{
    public class PendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;
        bool async;

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

        public PendingRequestQueueBuilder WithAsync(bool async)
        {
            this.async = async;
            return this;
        }

        public PendingRequestQueueBuilder WithAsync(ForceClientProxyType? forceClientProxyType)
        {
            async = forceClientProxyType == ForceClientProxyType.AsyncClient;
            return this;
        }

        public IPendingRequestQueue Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? HalibutLimits.PollingQueueWaitTimeout;
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            if (async)
            {
                return new PendingRequestQueueAsync(log, pollingQueueWaitTimeout);
            }

            return new PendingRequestQueue(log, pollingQueueWaitTimeout);
        }
    }
}