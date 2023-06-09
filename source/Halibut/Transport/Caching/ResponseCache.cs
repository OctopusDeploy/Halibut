using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut.Transport.Caching
{
    /// <summary>
    /// Allows the Response Caching behaviour to be modified for Error responses.
    /// By default all errors responses are not cached.
    /// </summary>
    /// <param name="responseMessage">The error response message</param>
    /// <returns>True to allow the error response message to be cached, otherwise false.</returns>
    public delegate bool OverrideErrorResponseMessageCachingAction(ResponseMessage responseMessage);

    internal class ResponseCache
    {
        readonly TypedMemoryCache<ServiceEndPoint, TypedMemoryCache<string, CacheItemWrapper>> responseMessageCache = new("ResponseMessageCache", point => point.BaseUri.AbsoluteUri + " " + point.RemoteThumbprint);

        TypedMemoryCache<string, CacheItemWrapper> CacheForEndpoint(ServiceEndPoint endPoint)
        {
            return responseMessageCache.GetOrAddNotAtomic(endPoint, new TypedMemoryCache<string, CacheItemWrapper>("ResponseMessageCache", s => s), new CacheItemPolicy() { });
        }
        public ResponseMessage GetCachedResponse(ServiceEndPoint endPoint, RequestMessage request, MethodInfo methodInfo)
        {
            var responseCanBeCached = CanBeCached(methodInfo);
            if (!responseCanBeCached) return null;

            var cacheKey = GetCacheKey(endPoint, methodInfo, request);
            var cacheForService = CacheForEndpoint(endPoint);
            var wrapper = cacheForService.GetCacheItem(cacheKey);

            return wrapper?.ResponseMessage;

        }

        public void CacheResponse(ServiceEndPoint endPoint, RequestMessage request, MethodInfo methodInfo, ResponseMessage response, OverrideErrorResponseMessageCachingAction overrideErrorResponseMessageCachingAction)
        {
            var responseCanBeCached = CanBeCached(methodInfo, response, overrideErrorResponseMessageCachingAction);
            if (!responseCanBeCached) return;

            FixMissingHalibutRuntimeProcessIdentifier(response);

            var cacheKey = GetCacheKey(endPoint, methodInfo, request);
            var cacheDuration = GetCacheDuration(methodInfo);

            var wrapper = new CacheItemWrapper
            {
                HalibutRuntimeUniqueIdentifier = response.HalibutRuntimeProcessIdentifier!.Value,
                ResponseMessage = response,
                EndPoint = endPoint
            };
            CacheForEndpoint(endPoint).Add(cacheKey, wrapper, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(cacheDuration) });
        }

        public void InvalidateStaleCachedResponses(ServiceEndPoint endPoint, ResponseMessage response)
        {
            FixMissingHalibutRuntimeProcessIdentifier(response);

            CacheForEndpoint(endPoint)
                .RemoveMatching((wrapper) => wrapper.EndPoint == endPoint && wrapper.HalibutRuntimeUniqueIdentifier != response.HalibutRuntimeProcessIdentifier);
        }

        bool CanBeCached(MethodInfo methodInfo)
        {
            return methodInfo.GetCustomAttribute<CacheResponseAttribute>() != null;
        }

        bool CanBeCached(MethodInfo methodInfo, ResponseMessage response, OverrideErrorResponseMessageCachingAction overrideErrorResponseMessageCachingAction)
        {
            if (!CanBeCached(methodInfo))
            {
                return false;
            }

            if (response.Error != null)
            {
                var overrideCacheResult = overrideErrorResponseMessageCachingAction?.Invoke(response);

                return overrideCacheResult ?? false;
            }

            return true;
        }

        void FixMissingHalibutRuntimeProcessIdentifier(ResponseMessage response)
        {
            // Halibut prior to caching being added will return null for HalibutRuntimeProcessIdentifier
            response.HalibutRuntimeProcessIdentifier ??= Guid.Empty;
        }

        int GetCacheDuration(MethodInfo methodInfo)
        {
            var cacheAttribute = methodInfo.GetCustomAttribute<CacheResponseAttribute>();

            return cacheAttribute?.DurationInSeconds ?? 0;
        }

        string GetCacheKey(ServiceEndPoint endpoint, MethodInfo methodInfo, RequestMessage request)
        {
            var arguments = ServiceInvoker.GetArguments(request, methodInfo);
            var cacheKey = new CachingKeyGenerator().GetCacheKey(methodInfo, arguments, null);

            return $"{endpoint.BaseUri.AbsoluteUri}:{cacheKey}";
        }

        class CacheItemWrapper
        {
            public ServiceEndPoint EndPoint { get; set; }
            public Guid HalibutRuntimeUniqueIdentifier { get; set; }
            public ResponseMessage ResponseMessage { get; set; }
        }
    }
}
