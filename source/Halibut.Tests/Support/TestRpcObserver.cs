using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestRpcObserver : IRpcObserver
    {
        readonly List<string> startCalls = new();
        readonly List<string> endCalls = new();

        public IReadOnlyList<string> StartCalls => startCalls;
        public IReadOnlyList<string> EndCalls => endCalls;

        public void StartCall(string methodName)
        {
            startCalls.Add(methodName);
        }

        public void StopCall(string methodName)
        {
            endCalls.Add(methodName);
        }
    }
}