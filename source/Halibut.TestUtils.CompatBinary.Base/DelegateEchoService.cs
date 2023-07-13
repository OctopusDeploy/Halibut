using System;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateEchoService : IEchoService
    {
        readonly IEchoService echoService;

        public DelegateEchoService(IEchoService echoService)
        {
            this.echoService = echoService;
        }

        public int LongRunningOperation()
        {
            Console.WriteLine("Forwarding LongRunningOperation() call to delegate");
            return echoService.LongRunningOperation();
        }

        public string SayHello(string name)
        {
            Console.WriteLine("Forwarding SayHello() call to delegate");
            return echoService.SayHello(name);
        }

        public bool Crash()
        {
            Console.WriteLine("Forwarding Crash() call to delegate");
            return echoService.Crash();
        }

        public int CountBytes(DataStream stream)
        {
            Console.WriteLine("Forwarding CountBytes() call to delegate");
            return echoService.CountBytes(stream);
        }
    }
}