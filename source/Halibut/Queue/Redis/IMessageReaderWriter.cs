using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis
{
    public interface IMessageReaderWriter
    {
        Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken);
        Task<RequestMessage> ReadRequest(string jsonRequest, CancellationToken cancellationToken);
        Task<string> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken);
        Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken);
    }
}