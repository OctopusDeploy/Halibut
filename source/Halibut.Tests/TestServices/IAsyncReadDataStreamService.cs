using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface IAsyncReadDataStreamService
    {
        Task<long> SendDataAsync(DataStream[] dataStreams, CancellationToken cancellationToken);
    }
}
