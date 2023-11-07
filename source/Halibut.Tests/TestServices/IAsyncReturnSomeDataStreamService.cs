using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface IAsyncReturnSomeDataStreamService
    {
        public Task<DataStream> SomeDataStreamAsync(CancellationToken cancellationToken);
    }
}