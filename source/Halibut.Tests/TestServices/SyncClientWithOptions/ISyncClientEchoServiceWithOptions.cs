using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface ISyncClientEchoServiceWithOptions
    {
        int LongRunningOperation(HalibutProxyRequestOptions halibutProxyRequestOptions);

        string SayHello(string name, HalibutProxyRequestOptions halibutProxyRequestOptions);

        bool Crash(HalibutProxyRequestOptions halibutProxyRequestOptions);

        int CountBytes(DataStream stream, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}