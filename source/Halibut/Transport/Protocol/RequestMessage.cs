using System;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class RequestMessage
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public override string ToString()
        {
            return Id;
        }
    }
}