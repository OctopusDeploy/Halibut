using System;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public class ResponseMessageFactory
    {
        public static IResponseMessage FromResult(IRequestMessage request, object result)
        {
            if (request is RequestMessage)
            {
                return new ResponseMessage { Id = request.Id, Result = result };
            }
            else if (request is RequestMessageV2)
            {
                return new ResponseMessageV2()
                {
                    Id = request.Id,
                    Result = result,
                    HalibutProcessIdentifier = Guid.NewGuid().ToString()
                };
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static IResponseMessage FromError(IRequestMessage request, string message)
        {
            if (request is RequestMessage)
            {
                return new ResponseMessage { Id = request.Id, Error = new ServerError { Message = message } };
            }
            else if (request is RequestMessageV2)
            {
                return new ResponseMessageV2
                {
                    Id = request.Id,
                    Error = new ServerError { Message = message },
                    HalibutProcessIdentifier = Guid.NewGuid().ToString()
                };
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static IResponseMessage FromException(IRequestMessage request, Exception ex)
        {
            if (request is null)
            {
                return new ResponseMessage { Id = "Unknown", Error = new ServerError { Message = ex.UnpackFromContainers().Message, Details = ex.ToString() } };
            }
            else if (request is RequestMessage)
            {
                return new ResponseMessage { Id = request.Id, Error = new ServerError { Message = ex.UnpackFromContainers().Message, Details = ex.ToString() } };
            }
            else if (request is RequestMessageV2)
            {
                return new ResponseMessageV2
                {
                    Id = request.Id,
                    Error = new ServerError { Message = ex.UnpackFromContainers().Message, Details = ex.ToString() },
                    HalibutProcessIdentifier = Guid.NewGuid().ToString()
                };
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}