using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Halibut.Diagnostics;
using Halibut.Exceptions;

namespace Halibut.Transport
{
    public interface IActiveTcpConnectionLease : IDisposable
    {
        /// <summary>
        /// The number of active TCP connections for the leased subscription, as at the point the lease was
        /// created. After Dispose() is called, this reflects the count immediately after this connection
        /// was released.
        /// </summary>
        int CurrentCount { get; }
    }

    public interface IActiveTcpConnectionsLimiter
    {
        IActiveTcpConnectionLease LeaseActiveTcpConnection(Uri subscriptionId);
    }

    public class ActiveTcpConnectionsLimiter : IActiveTcpConnectionsLimiter
    {
        readonly HalibutTimeoutsAndLimits timeoutsAndLimits;

        Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId = new();

        public ActiveTcpConnectionsLimiter(HalibutTimeoutsAndLimits timeoutsAndLimits)
        {
            this.timeoutsAndLimits = timeoutsAndLimits;
        }

        public IActiveTcpConnectionLease LeaseActiveTcpConnection(Uri subscriptionId)
        {
            //if there is no limit, then we still count the connection (callers rely on the resulting count),
            //we just never reject it
            if (!timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.HasValue)
            {
                return CreateUnlimitedLease(subscriptionId);
            }

            return new LimitingAuthorizedTcpConnectionLease(subscriptionId, activeConnectionCountPerSubscriptionId, timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.Value);
        }

        IActiveTcpConnectionLease CreateUnlimitedLease(Uri subscriptionId)
        {
            return new LimitingAuthorizedTcpConnectionLease(subscriptionId, activeConnectionCountPerSubscriptionId, int.MaxValue);
        }

        class LimitingAuthorizedTcpConnectionLease : IActiveTcpConnectionLease
        {
            readonly Uri subscriptionId;
            readonly Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId;

            public int CurrentCount { get; private set; }

            public LimitingAuthorizedTcpConnectionLease(Uri subscriptionId, Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId, int maximumAcceptedTcpConnectionsPerThumbprint)
            {
                this.subscriptionId = subscriptionId;
                this.activeConnectionCountPerSubscriptionId = activeConnectionCountPerSubscriptionId;

                lock (this.activeConnectionCountPerSubscriptionId)
                {
                    if (!this.activeConnectionCountPerSubscriptionId.TryGetValue(subscriptionId, out var count))
                    {
                        count = new StrongBox<int>(0);
                        this.activeConnectionCountPerSubscriptionId.Add(subscriptionId, count);
                    }

                    //validate the new count. If this throws an exception, it'll kill the connection
                    if (count.Value + 1 > maximumAcceptedTcpConnectionsPerThumbprint)
                    {
                        //throw an exception, bailing on the connection
                        throw new ActiveTcpConnectionsExceededException(this.subscriptionId, $"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of active TCP connections for subscription {subscriptionId}");
                    }

                    count.Value++;
                    CurrentCount = count.Value;
                }
            }

            public void Dispose()
            {
                lock (activeConnectionCountPerSubscriptionId)
                {
                    if (activeConnectionCountPerSubscriptionId.TryGetValue(subscriptionId, out var count))
                    {
                        //decrement the count of authorized connections
                        count.Value--;
                        CurrentCount = count.Value;

                        // Remove the key from the dictionary if the value is 0
                        if (count.Value == 0)
                        {
                            activeConnectionCountPerSubscriptionId.Remove(subscriptionId);
                        }
                    }
                }
            }
        }
    }
}