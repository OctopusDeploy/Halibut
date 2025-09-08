﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Halibut.Diagnostics;
using Halibut.Logging;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support.Logging
{
    public class TestContextLogFactory : ILogFactory
    {
        readonly Func<string, ILog> loggerFactory;
        readonly ConcurrentDictionary<string, ILog> loggers = new();
        readonly HashSet<Uri> endpoints = new();
        readonly HashSet<string> prefixes = new();

        public TestContextLogFactory(Func<string, ILog> loggerFactory)
        {
            this.loggerFactory = loggerFactory;
        }

        public TestContextLogFactory(string name, LogLevel logLevel)
        {
            loggerFactory = uri => new TestContextConnectionLog(uri, name, logLevel);
        }

        public Uri[] GetEndpoints()
        {
            lock (endpoints)
            {
                return endpoints.ToArray();
            }
        }

        public string[] GetPrefixes()
        {
            lock (prefixes)
            {
                return prefixes.ToArray();
            }
        }

        public ILog ForEndpoint(Uri endpoint)
        {
            endpoint = NormalizeEndpoint(endpoint);
            lock (endpoints)
            {
                endpoints.Add(endpoint);
            }

            return loggers.GetOrAdd(endpoint.ToString(), e => loggerFactory(e));
        }

        public ILog ForPrefix(string prefix)
        {
            lock (prefixes)
            {
                prefixes.Add(prefix);
            }

            return loggers.GetOrAdd(prefix, e => loggerFactory(e));
        }

        static Uri NormalizeEndpoint(Uri endpoint)
        {
            return ServiceEndPoint.IsWebSocketAddress(endpoint)
                ? new Uri(endpoint.AbsoluteUri.ToLowerInvariant())
                : new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/').ToLowerInvariant());
        }
    }
}