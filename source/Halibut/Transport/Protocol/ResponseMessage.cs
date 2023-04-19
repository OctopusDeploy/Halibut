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

        public static ResponseMessage FromResult(RequestMessage request, object result)
        {
            return new ResponseMessage { Id = request.Id, Result = result };
        }

        public static ResponseMessage FromError(RequestMessage request, string message)
        {
            return new ResponseMessage { Id = request.Id, Error = new ServerError { Message = message } };
        }

        public static ResponseMessage FromException(RequestMessage request, Exception ex)
        {
            return new ResponseMessage {Id = request.Id, Error = ServerErrorFromException(ex)};
        }

        internal static ServerError ServerErrorFromException(Exception ex)
        {
            string ErrorType = null;
            if (ex is HalibutClientException)
            {
                ErrorType = ex.GetType().FullName;
            }
            return new ServerError { Message = ex.UnpackFromContainers().Message, Details = ex.ToString(), ErrorType = ErrorType };
        }
    }
}