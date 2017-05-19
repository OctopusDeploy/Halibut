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

        public bool Crash()
        {
            throw new DivideByZeroException();
        }

        public static Action OnLongRunningOperation { get; set; }

        public int LongRunningOperation()
        {
            OnLongRunningOperation();
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
}