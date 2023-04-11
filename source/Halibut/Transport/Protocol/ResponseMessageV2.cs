using System;
using Halibut.Diagnostics;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ResponseMessageV2 : IResponseMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("error")]
        public ServerError Error { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        public string HalibutProcessIdentifier { get; set; }
    }
}