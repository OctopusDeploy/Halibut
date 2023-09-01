namespace Halibut.Transport.Observability
{
    public interface IRpcObserver
    {
        void StartCall(string methodName);
        void StopCall(string methodName);
    }
}