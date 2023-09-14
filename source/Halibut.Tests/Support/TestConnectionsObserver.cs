using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestConnectionsObserver : IConnectionsObserver
    {
        readonly ConcurrentBag<bool> connectionAcceptedAuthorized = new();
        readonly ConcurrentBag<bool> connectionClosedAuthorized = new();
        
        public long ConnectionAcceptedCount => connectionAcceptedAuthorized.Count;
        public long ConnectionClosedCount => connectionClosedAuthorized.Count;

        public IReadOnlyList<bool> ConnectionAcceptedAuthorized => connectionAcceptedAuthorized.ToList();
        public IReadOnlyList<bool> ConnectionClosedAuthorized => connectionClosedAuthorized.ToList();

        public void ConnectionAccepted(bool authorized)
        {
            connectionAcceptedAuthorized.Add(authorized);
        }

        public void ConnectionClosed(bool authorized)
        {
            connectionClosedAuthorized.Add(authorized);
        }
    }
}