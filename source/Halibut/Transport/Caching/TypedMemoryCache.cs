using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace Halibut.Transport.Caching
{
    public class TypedMemoryCache<K, V> where K: class where V : class
    {
        MemoryCache memoryCache;
        Func<K, string> toKeyFunction;

        public TypedMemoryCache(string name, Func<K, string> toKeyFunction)
        {
            this.toKeyFunction = toKeyFunction;
            memoryCache = new MemoryCache(name);
        }

        public V GetCacheItem(K cacheKey)
        {
            return memoryCache.GetCacheItem(toKeyFunction(cacheKey))?.Value as V;
        }

        public void Add(K cacheKey, V wrapper, CacheItemPolicy cacheItemPolicy)
        {
            memoryCache.Add(toKeyFunction(cacheKey), wrapper, cacheItemPolicy);
        }

        public V GetOrAddNotAtomic(K cacheKey, V valueIfOneDoesNotAlreadyExist, CacheItemPolicy policy)
        {
            var current = GetCacheItem(cacheKey);
            if (current != null) return current;
            Add(cacheKey, valueIfOneDoesNotAlreadyExist, policy);
            return GetCacheItem(cacheKey);
        }

        public void RemoveMatching(Func<V, bool> removeIfMatches)
        {
            var cacheItems = memoryCache.ToList();

            foreach (var item in cacheItems)
            {
                var wrapper = item.Value as V;

                if (removeIfMatches(wrapper))
                {
                    memoryCache.Remove(item.Key);
                }
            }
        }
    }
}