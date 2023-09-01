using System;

namespace Halibut.Transport.Observability
{
    public class NoRpcObserver : IRpcObserver
    {
        public void StartCall(string methodName)
        {
        }

        public void StopCall(string methodName)
        {
        }
    }
}