using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Diagnostics
{
    public class LogFactory : ILogFactory
    {
        readonly LogEventStorage logEventStorage = new LogEventStorage();
        readonly ConcurrentDictionary<string, InMemoryConnectionLog> events = new ConcurrentDictionary<string, InMemoryConnectionLog>();
        readonly HashSet<Uri> endpoints = new HashSet<Uri>();
        readonly HashSet<string> prefixes = new HashSet<string>();

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
            return events.GetOrAdd(endpoint.ToString(), e => new InMemoryConnectionLog(endpoint.ToString(), logEventStorage));
        }

        public ILog ForPrefix(string prefix)
        {
            lock (prefixes)
                prefixes.Add(prefix);
            return events.GetOrAdd(prefix, e => new InMemoryConnectionLog(prefix, logEventStorage));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}