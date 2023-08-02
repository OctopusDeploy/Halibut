using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncReadDataStreamService
    {
        Task<long> SendDataAsync(params DataStream[] dataStreams);
    }
}
