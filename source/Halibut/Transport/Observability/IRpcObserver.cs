using Halibut.Transport.Protocol;

namespace Halibut.Transport.Observability
{
    public interface IRpcObserver
    {
        void StartCall(RequestMessage request);
        void StopCall(RequestMessage request);
    }
}