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

                lock (this.activeConnectionCountPerSubscriptionId)
                {
                    if (!this.activeConnectionCountPerSubscriptionId.TryGetValue(subscriptionId, out var count))
                    {
                        count = new StrongBox<int>(0);
                        this.activeConnectionCountPerSubscriptionId.Add(subscriptionId, count);
                    }

                    var previousCount = count.Value;

                    //validate the new count. If this throws an exception, it'll kill the connection
                    if (count.Value + 1 > maximumAcceptedTcpConnectionsPerThumbprint)
                    {
                        //throw an exception, bailing on the connection
                        throw new ActiveTcpConnectionsExceededException(this.subscriptionId, $"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of active TCP connections for subscription {subscriptionId}");
                    }

                    count.Value++;

                    connectionsObserver.ConnectionsCountChangedFor(subscriptionId, previousCount, count.Value);
                }
            }

            public void Dispose()
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

                        connectionsObserver.ConnectionsCountChangedFor(subscriptionId, previousCount, count.Value);
                    }
                }
            }
        }
    }
}
