using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public class EchoService : IEchoService
    {
        public Task<string> SayHello(string name)
        {
            return Task.FromResult(name + "...");
        }

        public Task<bool> Crash()
        {
            throw new DivideByZeroException();
        }

        public static Action OnLongRunningOperation { get; set; }

        public async Task<int> LongRunningOperation()
        {
            OnLongRunningOperation();
            await Task.Delay(10000).ConfigureAwait(false);
            return 12;
        }

        public async Task<int> CountBytes(DataStream stream)
        {
            var tempFile = Path.GetFullPath(Guid.NewGuid().ToString());
            await stream.Receiver().SaveTo(tempFile).ConfigureAwait(false);
            var length = (int) new FileInfo(tempFile).Length;
            File.Delete(tempFile);

            return length;
        }
    }
}