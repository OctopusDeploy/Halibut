using System.Collections.Generic;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Support
{
    public class TestRpcObserver : IRpcObserver
    {
        readonly List<RequestMessage> startCalls = new();
        readonly List<RequestMessage> endCalls = new();

        public IReadOnlyList<RequestMessage> StartCalls => startCalls;
        public IReadOnlyList<RequestMessage> EndCalls => endCalls;

        public void StartCall(RequestMessage request)
        {
            startCalls.Add(request);
        }

        public void StopCall(RequestMessage request)
        {
            endCalls.Add(request);
        }
    }
}