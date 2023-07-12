using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Halibut.Diagnostics;

namespace Halibut.Tests.Support
{
    public class TestContextLogFactory : ILogFactory
    {
        readonly string name;
        readonly ConcurrentDictionary<string, TestContextConnectionLog> events = new ConcurrentDictionary<string, TestContextConnectionLog>();
        readonly HashSet<Uri> endpoints = new HashSet<Uri>();
        readonly HashSet<string> prefixes = new HashSet<string>();

        public TestContextLogFactory(string name)
        {
            this.name = name;
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
            return events.GetOrAdd(endpoint.ToString(), e => new TestContextConnectionLog(endpoint.ToString(), name));
        }

        public ILog ForPrefix(string prefix)
        {
            lock (prefixes)
                prefixes.Add(prefix);
            return events.GetOrAdd(prefix, e => new TestContextConnectionLog(prefix, name));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}