using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Halibut.Diagnostics
{
    public class LogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<Uri, InMemoryConnectionLog> events = new ConcurrentDictionary<Uri, InMemoryConnectionLog>();

        public Uri[] GetEndpoints()
        {
            return events.Keys.ToArray();
        }

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = NormalizeEndpoint(endpoint);
            return events.GetOrAdd(endpoint, e => new InMemoryConnectionLog(endpoint.ToString()));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return new Uri(endpoint.GetLeftPart(UriPartial.Authority).TrimEnd('/').ToLowerInvariant());
        }
    }
}