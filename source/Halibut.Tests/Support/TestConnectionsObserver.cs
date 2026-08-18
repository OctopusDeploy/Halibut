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
        readonly ConcurrentBag<(Uri SubscriptionId, int CurrentCount)> connectionAcceptedForSubscriptions = new();
        readonly ConcurrentBag<(Uri SubscriptionId, int CurrentCount)> connectionClosedForSubscriptions = new();

        public long ConnectionAcceptedCount => connectionAcceptedAuthorized.Count;
        public long ConnectionClosedCount => connectionClosedAuthorized.Count;

        public IReadOnlyList<bool> ConnectionAcceptedAuthorized => connectionAcceptedAuthorized.ToList();
        public IReadOnlyList<bool> ConnectionClosedAuthorized => connectionClosedAuthorized.ToList();
        public IReadOnlyList<(Uri SubscriptionId, int CurrentCount)> ConnectionAcceptedForSubscriptions => connectionAcceptedForSubscriptions.ToList();
        public IReadOnlyList<(Uri SubscriptionId, int CurrentCount)> ConnectionClosedForSubscriptions => connectionClosedForSubscriptions.ToList();

        public void ConnectionAccepted(bool authorized)
        {
            connectionAcceptedAuthorized.Add(authorized);
        }

        public void ConnectionClosed(bool authorized)
        {
            connectionClosedAuthorized.Add(authorized);
        }

        public void ConnectionAcceptedFor(Uri subscriptionId, int currentCount)
        {
            connectionAcceptedForSubscriptions.Add((subscriptionId, currentCount));
        }

        public void ConnectionClosedFor(Uri subscriptionId, int currentCount)
        {
            connectionClosedForSubscriptions.Add((subscriptionId, currentCount));
        }
    }
}