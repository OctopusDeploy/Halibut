using System;
using Halibut.Diagnostics;

namespace Halibut.Tests.Builders
{
    public interface IPendingRequestQueueBuilder
    {
        public IPendingRequestQueueBuilder WithEndpoint(string endpoint);
        public IPendingRequestQueueBuilder WithLog(ILog log);
        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout);
        public IPendingRequestQueueBuilder WithDelayBeforeCheckingForCancellation(TimeSpan defaultDelayBeforeSubscribingToRequestCancellation);
        public QueueHolder Build();
    }
}