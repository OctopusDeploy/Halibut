using System;
using Halibut.ServiceModel;
using Halibut.Transport.Caching;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface IClientCachingService
    {
        Guid NonCachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);

        [CacheResponse(600)]
        Guid CachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}