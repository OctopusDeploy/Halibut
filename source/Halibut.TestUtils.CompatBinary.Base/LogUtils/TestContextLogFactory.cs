using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Halibut.Diagnostics;
using Halibut.Logging;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    public class TestContextLogFactory : ILogFactory
    {
        readonly string name;
        readonly LogLevel logLevel;
        readonly ConcurrentDictionary<string, TestContextConnectionLog> events = new();
        readonly HashSet<Uri> endpoints = new();
        readonly HashSet<string> prefixes = new();

        public TestContextLogFactory(string name, LogLevel logLevel)
        {
            this.name = name;
            this.logLevel = logLevel;
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
            return events.GetOrAdd(endpoint.ToString(), e => new TestContextConnectionLog(endpoint.ToString(), name, logLevel));
        }

        public ILog ForPrefix(string prefix)
        {
            lock (prefixes)
                prefixes.Add(prefix);
            return events.GetOrAdd(prefix, e => new TestContextConnectionLog(prefix, name, logLevel));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}