using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Halibut.Diagnostics
{
    public class LogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<string, InMemoryConnectionLog> events = new ConcurrentDictionary<string, InMemoryConnectionLog>();
        readonly ConcurrentBag<Uri> endpoints = new ConcurrentBag<Uri>();
        readonly ConcurrentBag<string> prefixes = new ConcurrentBag<string>();

        public Uri[] GetEndpoints() => endpoints.ToArray();
        public string[] GetPrefixes() => prefixes.ToArray();

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = NormalizeEndpoint(endpoint);
            endpoints.Add(endpoint);
            return events.GetOrAdd(endpoint.ToString(), e => new InMemoryConnectionLog(endpoint.ToString()));
        }

        public ILog ForPrefix(string prefix)
        {
            prefixes.Add(prefix);
            return events.GetOrAdd(prefix, e => new InMemoryConnectionLog(prefix));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return endpoint.Scheme == "wss" 
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant()) 
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}