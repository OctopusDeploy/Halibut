using System;

namespace Halibut.Transport.Caching
{
    public interface ICachable
    {
        /// <summary>
        /// Gets the cache key.
        /// </summary>
        /// <value>The cache key.</value>
        string CacheKey { get; }
    }
}