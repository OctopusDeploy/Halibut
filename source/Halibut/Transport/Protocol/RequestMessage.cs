using System;
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

        internal Action<ResponseMessage> ResponseArrived = _ => { };
        
        internal void SetResponse(ResponseMessage response)
        {
            ResponseArrived(response);
        }
    }
}