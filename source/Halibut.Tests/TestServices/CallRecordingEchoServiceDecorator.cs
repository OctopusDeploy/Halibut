using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class CallRecordingEchoServiceDecorator : IEchoService
    {

        public int SayHelloCallCount { get; set; } = 0;
        
        IEchoService echoService;

        public CallRecordingEchoServiceDecorator(IEchoService echoService)
        {
            this.echoService = echoService;
        }

        public int LongRunningOperation()
        {
            return echoService.LongRunningOperation();
        }

        public string SayHello(string name)
        {
            SayHelloCallCount++;
            return echoService.SayHello(name);
        }

        public bool Crash()
        {
            return echoService.Crash();
        }

        public int CountBytes(DataStream stream)
        {
            return echoService.CountBytes(stream);
        }
    }
}
