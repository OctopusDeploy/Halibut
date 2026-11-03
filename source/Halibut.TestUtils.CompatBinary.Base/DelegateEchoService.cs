using System;
using System.IO;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;
using Halibut.TestUtils.SampleProgram.Base.Utils;

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
            NonBlockingConsole.WriteLine("Forwarding LongRunningOperation() call to delegate");
            return echoService.LongRunningOperation();
        }

        public string SayHello(string name)
        {
            NonBlockingConsole.WriteLine("Forwarding SayHello() call to delegate");
            return echoService.SayHello(name);
        }

        public bool Crash()
        {
            NonBlockingConsole.WriteLine("Forwarding Crash() call to delegate");
            try
            {
                return echoService.Crash();
            }
            catch (Exception e)
            {
                throw new Exception("Received Exception: START:\n" +
                                    e.Message
                                    + "\nEnd Received Exception");
            }
        }

        public int CountBytes(DataStream dataStream)
        {
            dataStream = dataStream.ConfigureWriterOnReceivedDataStream();
            NonBlockingConsole.WriteLine("Forwarding CountBytes() call to delegate");
            return echoService.CountBytes(dataStream);
        }
    }
}
