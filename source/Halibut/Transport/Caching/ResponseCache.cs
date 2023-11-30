using System;
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

    class ResponseCache : IDisposable
    {
        readonly MemoryCache responseMessageCache = new("ResponseMessageCache");

        public ResponseMessage? GetCachedResponse(ServiceEndPoint endPoint, RequestMessage request, MethodInfo methodInfo)
        {
            var responseCanBeCached = CanBeCached(methodInfo);
            if (!responseCanBeCached) return null;

            var cacheKey = GetCacheKey(endPoint, methodInfo, request);
            var wrapper = responseMessageCache.GetCacheItem(cacheKey)?.Value as CacheItemWrapper;

            return wrapper?.ResponseMessage;

        }

        public void CacheResponse(ServiceEndPoint endPoint, RequestMessage request, MethodInfo methodInfo, ResponseMessage response, OverrideErrorResponseMessageCachingAction? overrideErrorResponseMessageCachingAction)
        {
            var responseCanBeCached = CanBeCached(methodInfo, response, overrideErrorResponseMessageCachingAction);
            if (!responseCanBeCached) return;

            var cacheKey = GetCacheKey(endPoint, methodInfo, request);
            var cacheDuration = GetCacheDuration(methodInfo);

            var wrapper = new CacheItemWrapper(endPoint, response);
            responseMessageCache.Add(cacheKey, wrapper, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(cacheDuration) });
        }

        bool CanBeCached(MethodInfo methodInfo)
        {
            return methodInfo.GetCustomAttribute<CacheResponseAttribute>() != null;
        }

        bool CanBeCached(MethodInfo methodInfo, ResponseMessage response, OverrideErrorResponseMessageCachingAction? overrideErrorResponseMessageCachingAction)
        {
            if (response == null)
            {
                return false;
            }
            
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
            public ServiceEndPoint EndPoint { get; }
            public ResponseMessage ResponseMessage { get; }

            public CacheItemWrapper(ServiceEndPoint endPoint, ResponseMessage responseMessage)
            {
                EndPoint = endPoint;
                ResponseMessage = responseMessage;
            }
        }

        public void Dispose()
        {
            responseMessageCache?.Dispose();
        }
    }
}
