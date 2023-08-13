using System;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface ISyncClientDoSomeActionServiceWithOptions
    {
        void Action(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}
