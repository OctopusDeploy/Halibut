using System;
using System.Collections.Concurrent;
using Halibut.Diagnostics.LogCreators;

namespace Halibut.Diagnostics
{
    public class CachingLogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<string, ILog> cache = new();
        readonly ICreateNewILog logCreator;

        public CachingLogFactory(ICreateNewILog logCreator)
        {
            this.logCreator = logCreator;
        }

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = LogEndpointNormalizer.NormalizeEndpointForLogging(endpoint);
            return cache.GetOrAdd(endpoint.ToString(), logCreator.CreateNewForPrefix);
        }

        public ILog ForPrefix(string prefix)
        {
            return cache.GetOrAdd(prefix, logCreator.CreateNewForPrefix);
        }
    }
}