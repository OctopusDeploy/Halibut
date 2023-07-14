using Halibut.Transport.Protocol;
using System;

namespace Halibut.Tests.Builders
{
    public class RequestMessageBuilder
    {
        readonly ServiceEndPointBuilder serviceEndPointBuilder = new();

        public RequestMessageBuilder(string endpoint)
        {
            serviceEndPointBuilder.WithEndpoint(endpoint);
        }
        
        public RequestMessageBuilder WithServiceEndpoint(Action<ServiceEndPointBuilder> builderAction)
        {
            builderAction(serviceEndPointBuilder);
            return this;
        }
        
        public RequestMessage Build()
        {
            var serviceEndPoint = serviceEndPointBuilder.Build();

            var request = new RequestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Destination = serviceEndPoint
            };

            

            return request;
        }
    }
}