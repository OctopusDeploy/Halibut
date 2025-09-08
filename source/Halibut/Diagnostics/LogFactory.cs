using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Diagnostics
{
    public class LogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<string, InMemoryConnectionLog> events = new();
        readonly HashSet<Uri> endpoints = new();
        readonly HashSet<string> prefixes = new();
        readonly Logging.ILog logger;

        public LogFactory()
        {
            logger = LogProvider.GetLogger("Halibut");
        }

        public Uri[] GetEndpoints()
        {
            lock (endpoints)
                return endpoints.ToArray();
        }

        public string[] GetPrefixes()
        {
            lock (prefixes)
                return prefixes.ToArray();
        }

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = NormalizeEndpoint(endpoint);
            lock (endpoints)
                endpoints.Add(endpoint);
            return events.GetOrAdd(endpoint.ToString(), e => new InMemoryConnectionLog(endpoint.ToString(), logger));
        }

        public ILog ForPrefix(string prefix)
        {
            lock (prefixes)
                prefixes.Add(prefix);
            return events.GetOrAdd(prefix, e => new InMemoryConnectionLog(prefix, logger));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}