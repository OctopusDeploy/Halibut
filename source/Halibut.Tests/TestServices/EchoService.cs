using System;
using System.Threading;
using Halibut.Protocol;

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

        public int CountBytes(DataStream stream)
        {
            int read = 0;
            stream.Read(s =>
            {
                while (s.ReadByte() != -1)
                {
                    read++;
                }
            });

            return read;
        }
    }
}