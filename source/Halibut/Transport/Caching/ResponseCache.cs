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
        readonly TypedMemoryCache<string, CacheItemWrapper> responseMessageCache = new("ResponseMessageCache", k => k);
        readonly TypedMemoryCache<ServiceEndPoint, Guid> serviceToGuid = new("ResponseMessageCache", point => point.BaseUri.AbsoluteUri + " " + point.RemoteThumbprint); // Do we really need thumbprint as well?

        
        public ResponseMessage GetCachedResponse(ServiceEndPoint endPoint, RequestMessage request, MethodInfo methodInfo)
        {
            var responseCanBeCached = CanBeCached(methodInfo);
            if (!responseCanBeCached) return null;

            var cacheKey = GetCacheKey(endPoint, methodInfo, request);
            if (!responseMessageCache.TryGetCacheItem(cacheKey, out var wrapper))
            {
                // Its not in the cache
                return null;
            }
            
            // But is it valid?
            if (!serviceToGuid.TryGetCacheItem(endPoint, out var guid))
            {
                // The guid was dropped so we don't know if the value in the cache is valid.
                return null;
            }

            if (!wrapper!.HalibutRuntimeUniqueIdentifier.Equals(guid))
            {
                // What we have in cache was from a different instance of the service, so it may be out of date.
                return null;
            }
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
            
            responseMessageCache.Add(cacheKey, wrapper, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(cacheDuration) });
            serviceToGuid.Add(endPoint, wrapper.HalibutRuntimeUniqueIdentifier, new CacheItemPolicy());
        }

        public void InvalidateStaleCachedResponses(ServiceEndPoint endPoint, ResponseMessage response)
        {
            FixMissingHalibutRuntimeProcessIdentifier(response);
            // Could this be faster?
            serviceToGuid.Add(endPoint, response.HalibutRuntimeProcessIdentifier!.Value, new CacheItemPolicy());
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
