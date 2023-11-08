using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncEchoService : IAsyncEchoService
    {
        
        public async Task<int> LongRunningOperationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10000, cancellationToken);
            return 16;
        }

        public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return name + "...";
        }

        public async Task<bool> CrashAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new DivideByZeroException();
        }

        public async Task<int> CountBytesAsync(DataStream dataStream, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            var tempFile = Path.GetFullPath(Guid.NewGuid().ToString());
            await dataStream.Receiver().SaveToAsync(tempFile, cancellationToken);
            var length = (int) new FileInfo(tempFile).Length;
            File.Delete(tempFile);
            return length;
        }
    }
}

