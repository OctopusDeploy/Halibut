using System;
using Halibut.Diagnostics;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class RequestMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("activityId")]
        public Guid ActivityId { get; set; }

        [JsonProperty("endpoint")]
        public ServiceEndPoint Destination { get; set; }

        [JsonProperty("service")]
        public string ServiceName { get; set; }

        [JsonProperty("method")]
        public string MethodName { get; set; }

        [JsonProperty("params")]
        public object[] Params { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}