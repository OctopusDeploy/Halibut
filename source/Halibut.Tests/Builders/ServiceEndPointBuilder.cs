using Halibut.Transport.Protocol;
using System;

namespace Halibut.Tests.Builders
{
    public class ServiceEndPointBuilder
    {
        string? endpoint;
        TimeSpan? pollingRequestQueueTimeout;
        TimeSpan? pollingRequestMaximumMessageProcessingTimeout;

        public ServiceEndPointBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public ServiceEndPointBuilder WithPollingRequestQueueTimeout(TimeSpan timeout)
        {
            pollingRequestQueueTimeout = timeout;
            return this;
        }

        public ServiceEndPointBuilder WithPollingRequestMaximumMessageProcessingTimeout(TimeSpan timeout)
        {
            pollingRequestMaximumMessageProcessingTimeout = timeout;
            return this;
        }

        public ServiceEndPoint Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";

            var serviceEndPoint = new ServiceEndPoint(new Uri(endpoint), "thumbprint");
            if (pollingRequestQueueTimeout is not null)
            {
                serviceEndPoint.PollingRequestQueueTimeout = pollingRequestQueueTimeout.Value;
            }
            if (pollingRequestMaximumMessageProcessingTimeout is not null)
            {
                serviceEndPoint.PollingRequestMaximumMessageProcessingTimeout = pollingRequestMaximumMessageProcessingTimeout.Value;
            }

            return serviceEndPoint;
        }
    }
}