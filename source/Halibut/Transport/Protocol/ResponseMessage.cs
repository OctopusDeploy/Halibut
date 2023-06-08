using System;
using Halibut.Diagnostics;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ResponseMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("error")]
        public ServerError Error { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        [JsonProperty("halibutRuntimeProcessIdentifier")]
        public Guid? HalibutRuntimeProcessIdentifier { get; set; }

        public static ResponseMessage FromResult(RequestMessage request, object result, Guid halibutRuntimeProcessIdentifier)
        {
            return new ResponseMessage
            {
                Id = request.Id,
                Result = result,
                HalibutRuntimeProcessIdentifier = halibutRuntimeProcessIdentifier
            };
        }

        public static ResponseMessage FromError(RequestMessage request, string message, Guid halibutRuntimeProcessIdentifier)
        {
            return new ResponseMessage
            {
                Id = request.Id,
                Error = new ServerError
                {
                    Message = message
                },
                HalibutRuntimeProcessIdentifier = halibutRuntimeProcessIdentifier
            };
        }

        public static ResponseMessage FromException(RequestMessage request, Exception ex)
        {
            return FromException(request, ex, null);
        }

        public static ResponseMessage FromException(RequestMessage request, Exception ex, Guid? halibutRuntimeProcessIdentifier)
        {
            return new ResponseMessage
            {
                Id = request.Id,
                Error = ServerErrorFromException(ex),
                HalibutRuntimeProcessIdentifier = halibutRuntimeProcessIdentifier
            };
        }

        internal static ServerError ServerErrorFromException(Exception ex)
        {
            string errorType = null;
            if (ex is HalibutClientException)
            {
                errorType = ex.GetType().FullName;
            }

            return new ServerError
            {
                Message = ex.UnpackFromContainers().Message,
                Details = ex.ToString(),
                HalibutErrorType = errorType
            };
        }
    }
}