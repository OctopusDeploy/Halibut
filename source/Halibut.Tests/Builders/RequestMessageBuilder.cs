using Halibut.Transport.Protocol;
using System;

namespace Halibut.Tests.Builders
{
    public class RequestMessageBuilder
    {
        readonly ServiceEndPointBuilder serviceEndPointBuilder = new();
        private Guid? activityId;

        public RequestMessageBuilder(string endpoint)
        {
            serviceEndPointBuilder.WithEndpoint(endpoint);
        }
        
        public RequestMessageBuilder WithServiceEndpoint(Action<ServiceEndPointBuilder> builderAction)
        {
            builderAction(serviceEndPointBuilder);
            return this;
        }
        
        public RequestMessageBuilder WithActivityId(Guid activityId)
        {
            this.activityId = activityId;
            return this;
        }
        
        public RequestMessage Build()
        {
            var serviceEndPoint = serviceEndPointBuilder.Build();

            var request = new RequestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Destination = serviceEndPoint,
                ActivityId = activityId ?? Guid.NewGuid(),
            };

            return request;
        }
    }
}