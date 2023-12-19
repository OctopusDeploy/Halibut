using System;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ServerError
    {
        [JsonProperty("message")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string Message { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [JsonProperty("details")]
        public string? Details { get; set; }
        
        public string? HalibutErrorType { get; set; }

        [JsonIgnore]
        public ConnectionState ConnectionState { get; set; } = ConnectionState.Unknown;
    }
}