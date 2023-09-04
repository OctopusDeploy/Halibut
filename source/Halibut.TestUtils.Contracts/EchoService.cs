using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class EchoService : IEchoService
    {
        public string SayHello(string name)
        {
            return name + "...";
        }

        public bool Crash()
        {
            throw new DivideByZeroException();
        }

        public int LongRunningOperation()
        {
            Thread.Sleep(10000);
            return 12;
        }

        public int CountBytes(DataStream stream)
        {
            var tempFile = Path.GetFullPath(Guid.NewGuid().ToString());
            stream.Receiver().SaveTo(tempFile);
            var length = (int) new FileInfo(tempFile).Length;
            File.Delete(tempFile);
            return length;
        }
    }

    public class AsyncEchoService : IAsyncEchoService
    {
        readonly EchoService service = new EchoService();
        
        public async Task<int> LongRunningOperationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10000, cancellationToken);
            return 16;
        }

        public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.SayHello(name + "Async");
        }

        public async Task<bool> CrashAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.Crash();
        }

        public async Task<int> CountBytesAsync(DataStream dataStream, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.CountBytes(dataStream);
        }
    }
}

