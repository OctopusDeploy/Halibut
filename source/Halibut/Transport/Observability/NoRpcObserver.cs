using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport.Observability
{
    public class NoRpcObserver : IRpcObserver
    {
        public void StartCall(RequestMessage request)
        {
        }

        public void StopCall(RequestMessage request)
        {
        }
    }
}