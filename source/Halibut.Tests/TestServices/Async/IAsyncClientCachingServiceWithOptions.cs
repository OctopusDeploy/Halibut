using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Transport.Caching;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface IAsyncClientCachingServiceWithOptions
    {
        Task<Guid> NonCachableCallAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);

        [CacheResponse(600)]
        Task<Guid> CachableCallAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}