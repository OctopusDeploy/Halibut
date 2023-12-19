using System;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ResponseMessage
    {
        [JsonProperty("id")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string Id { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [JsonProperty("error")]
        public ServerError? Error { get; set; }

        [JsonProperty("result")]
        public object? Result { get; set; }

        public static ResponseMessage FromResult(RequestMessage request, object? result)
        {
            return new ResponseMessage { Id = request.Id, Result = result };
        }

        public static ResponseMessage FromError(RequestMessage request, string message)
        {
            return new ResponseMessage { Id = request.Id, Error = new ServerError { Message = message } };
        }

        public static ResponseMessage FromException(RequestMessage request, Exception ex, ConnectionState connectionState = ConnectionState.Unknown)
        {
            return new ResponseMessage { Id = request.Id, Error = ServerErrorFromException(ex, connectionState) };
        }

        internal static ServerError ServerErrorFromException(Exception ex, ConnectionState connectionState = ConnectionState.Unknown)
        {
            string? errorType = null;

            if (ex is HalibutClientException or RequestCancelledException)
            {
                errorType = ex.GetType().FullName;
            }

            return new ServerError
            {
                Message = ex.UnpackFromContainers().Message, 
                Details = ex.ToString(), 
                HalibutErrorType = errorType,
                ConnectionState = connectionState
            };
        }
    }
}