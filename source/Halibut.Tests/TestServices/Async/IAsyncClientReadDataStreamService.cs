using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientReadDataStreamService
    {
        Task<long> SendDataAsync(params DataStream[] dataStreams);
    }
}
