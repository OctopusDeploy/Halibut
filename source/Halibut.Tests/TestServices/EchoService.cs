using System;
using System.IO;
using System.Threading;

namespace Halibut.Tests.TestServices
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

        public static Action OnLongRunningOperation { get; set; }

        public int LongRunningOperation()
        {
            OnLongRunningOperation();
            Thread.Sleep(10000);
            return 12;
        }
    }
}