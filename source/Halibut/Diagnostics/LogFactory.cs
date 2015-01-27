using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Halibut.Diagnostics
{
    public class LogFactory : ILogFactory
    {
        readonly ConcurrentDictionary<string, InMemoryConnectionLog> events = new ConcurrentDictionary<string, InMemoryConnectionLog>();

        public string[] GetEndpoints()
        {
            return events.Keys.ToArray();
        }

        public ILog ForEndpoint(string endpoint)
        {
            return events.GetOrAdd(endpoint, e => new InMemoryConnectionLog(endpoint));
        }
    }
}