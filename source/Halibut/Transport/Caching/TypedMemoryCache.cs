#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Caching;

namespace Halibut.Transport.Caching
{
    internal class TypedMemoryCache<K, V> where K : class
    {
        readonly MemoryCache memoryCache;
        readonly Func<K, string> toKeyFunction;

        internal TypedMemoryCache(string name, Func<K, string> toKeyFunction)
        {
            this.toKeyFunction = toKeyFunction;
            memoryCache = new MemoryCache(name);
        }

        internal bool TryGetCacheItem(K cacheKey, out V? value)
        {
            var cacheItem = memoryCache.GetCacheItem(toKeyFunction(cacheKey));
            if (cacheItem == null)
            {
                value = default;
                return false;
            }

            value = (V) cacheItem.Value;
            return false;
        }

        internal void Add(K cacheKey, V wrapper, CacheItemPolicy cacheItemPolicy)
        {
            memoryCache.Add(toKeyFunction(cacheKey), wrapper, cacheItemPolicy);
        }

        internal V GetOrAddNotAtomic(K cacheKey, V valueIfOneDoesNotAlreadyExist, CacheItemPolicy policy)
        {
            if (TryGetCacheItem(cacheKey, out var current)) return current!;
            if (current != null) return current;
            Add(cacheKey, valueIfOneDoesNotAlreadyExist, policy);
            return valueIfOneDoesNotAlreadyExist;
        }
    }
}