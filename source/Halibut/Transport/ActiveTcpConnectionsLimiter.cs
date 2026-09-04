using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport.Observability;

namespace Halibut.Transport
{
    public interface IActiveTcpConnectionsLimiter
    {
        IDisposable LeaseActiveTcpConnection(Uri subscriptionId);
    }

    public class ActiveTcpConnectionsLimiter : IActiveTcpConnectionsLimiter
    {
        readonly HalibutTimeoutsAndLimits timeoutsAndLimits;
        readonly IConnectionsObserver connectionsObserver;

        Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId = new();

        public ActiveTcpConnectionsLimiter(HalibutTimeoutsAndLimits timeoutsAndLimits, IConnectionsObserver connectionsObserver)
        {
            this.timeoutsAndLimits = timeoutsAndLimits;
            this.connectionsObserver = connectionsObserver;
        }

        public IDisposable LeaseActiveTcpConnection(Uri subscriptionId)
        {
            //if there is no limit, then we still count the connection (the observer is told about every
            //connection either way), we just never reject it
            if (!timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.HasValue)
            {
                return CreateUnlimitedLease(subscriptionId);
            }

            return new LimitingAuthorizedTcpConnectionLease(subscriptionId, activeConnectionCountPerSubscriptionId, timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.Value, connectionsObserver);
        }

        IDisposable CreateUnlimitedLease(Uri subscriptionId)
        {
            return new LimitingAuthorizedTcpConnectionLease(subscriptionId, activeConnectionCountPerSubscriptionId, int.MaxValue, connectionsObserver);
        }

        class LimitingAuthorizedTcpConnectionLease : IDisposable
        {
            readonly Uri subscriptionId;
            readonly Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId;
            readonly IConnectionsObserver connectionsObserver;

            public LimitingAuthorizedTcpConnectionLease(Uri subscriptionId, Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId, int maximumAcceptedTcpConnectionsPerThumbprint, IConnectionsObserver connectionsObserver)
            {
                this.subscriptionId = subscriptionId;
                this.activeConnectionCountPerSubscriptionId = activeConnectionCountPerSubscriptionId;
                this.connectionsObserver = connectionsObserver;

                var (previousCount, currentCount) = IncrementCount( maximumAcceptedTcpConnectionsPerThumbprint);

                // Calling observer outside the lock might lead to surprising short term values but it will reduce the impact of a slow observer on the connection acceptance.
                // E.g. (1, 2) might be processed before (0, 1) which will result in (-1, 1) -> (0, 1) values in the bucket.
                // (-1, 1) should not really last for long as the connections are rather long-lived.
                connectionsObserver.ConnectionsCountChangedFor(subscriptionId, previousCount, currentCount);
            }

            (int previousCount, int currentCount) IncrementCount(int maximumAcceptedTcpConnectionsPerThumbprint)
            {
                lock (activeConnectionCountPerSubscriptionId)
                {
                    if (!activeConnectionCountPerSubscriptionId.TryGetValue(subscriptionId, out var count))
                    {
                        count = new StrongBox<int>(0);
                        activeConnectionCountPerSubscriptionId.Add(subscriptionId, count);
                    }

                    var previousCount = count.Value;

                    //validate the new count. If this throws an exception, it'll kill the connection
                    if (count.Value + 1 > maximumAcceptedTcpConnectionsPerThumbprint)
                    {
                        //throw an exception, bailing on the connection
                        throw new ActiveTcpConnectionsExceededException(subscriptionId, $"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of active TCP connections for subscription {subscriptionId}");
                    }

                    count.Value++;

                    return (previousCount, count.Value);
                }
            }

            public void Dispose()
            {
                var counts = DecrementCount();
                if (counts == null) return;

                var (previousCount, currentCount) = counts.Value;
                connectionsObserver.ConnectionsCountChangedFor(subscriptionId, previousCount, currentCount);
            }

            (int previousCount, int currentCount)? DecrementCount()
            {
                lock (activeConnectionCountPerSubscriptionId)
                {
                    if (activeConnectionCountPerSubscriptionId.TryGetValue(subscriptionId, out var count))
                    {
                        //decrement the count of authorized connections
                        var previousCount = count.Value;
                        count.Value--;

                        // Remove the key from the dictionary if the value is 0
                        if (count.Value == 0)
                        {
                            activeConnectionCountPerSubscriptionId.Remove(subscriptionId);
                        }

                        return (previousCount, count.Value);
                    }
                }
                return null;
            }
        }
    }
}
