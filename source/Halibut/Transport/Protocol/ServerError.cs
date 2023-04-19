using System;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ServerError
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }
        
        public string ErrorType { get; set; }
    }
}