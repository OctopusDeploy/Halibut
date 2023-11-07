using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public class AsyncReturnSomeDataStreamService : IAsyncReturnSomeDataStreamService
    {
        readonly Func<DataStream> createDataStream;

        public AsyncReturnSomeDataStreamService(Func<DataStream> createDataStream)
        {
            this.createDataStream = createDataStream;
        }

        public async Task<DataStream> SomeDataStreamAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return createDataStream();
        }
    }
}