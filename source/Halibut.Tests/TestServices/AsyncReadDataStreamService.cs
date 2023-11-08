using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncReadDataStreamService : IAsyncReadDataStreamService
    {
        async Task<long> SendDataAsync(DataStream dataStream, CancellationToken cancellationToken)
        {
            long total = 0;
            await dataStream.Receiver().ReadAsync(async (reader, ct) =>
            {
                var buf = new byte[1024];
                while (true)
                {
                    int read = await reader.ReadAsync(buf, 0, buf.Length, ct);
                    if(read == 0) break;
                    total += read;
                }
            }, cancellationToken);

            return total;
        }

        public async Task<long> SendDataAsync(DataStream[] dataStreams, CancellationToken cancellationToken)
        {
            long count = 0;
            foreach (var dataStream in dataStreams)
            {
                count += await SendDataAsync(dataStream, cancellationToken);
            }

            return count;
        }
    }
}
