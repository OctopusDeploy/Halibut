using System;
using Newtonsoft.Json;

namespace Halibut.Protocol
{
    public class IdentificationMessage
    {
        public IdentificationMessage()
        {
            ProtocolVersion = 1;
            Subscription = null;
        }

        public IdentificationMessage(Uri subscription) : this()
        {
            Subscription = subscription;
        }

        [JsonProperty("protocolVersion")]
        public int ProtocolVersion { get; set; }

        [JsonProperty("subscription")]
        public Uri Subscription { get; set; }
    }
}