using System;
using System.Collections.Concurrent;
using Halibut.Diagnostics.LogCreators;

namespace Halibut.Diagnostics
{
    public class CachingLogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<string, ILog> cache = new();
        readonly ICreateNewILog logFactory;

        public CachingLogFactory(ICreateNewILog logFactory)
        {
            this.logFactory = logFactory;
        }

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = LogEndpointNormalizer.NormalizeEndpoint(endpoint);
            return cache.GetOrAdd(endpoint.ToString(), logFactory.CreateNewForPrefix);
        }

        public ILog ForPrefix(string prefix)
        {
            return cache.GetOrAdd(prefix, logFactory.CreateNewForPrefix);
        }
    }
}