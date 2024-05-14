using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Halibut.Diagnostics;
using Halibut.Exceptions;

namespace Halibut.Transport
{
    public interface IActiveTcpConnectionsLimiter
    {
        IDisposable LeaseActiveTcpConnection(Uri subscriptionId);

        IDisposable CreateUnlimitedLease();
    }

    public class ActiveTcpConnectionsLimiter : IActiveTcpConnectionsLimiter
    {
        readonly HalibutTimeoutsAndLimits timeoutsAndLimits;

        Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId = new();

        public ActiveTcpConnectionsLimiter(HalibutTimeoutsAndLimits timeoutsAndLimits)
        {
            this.timeoutsAndLimits = timeoutsAndLimits;
        }

        public IDisposable LeaseActiveTcpConnection(Uri subscriptionId)
        {
            //if there is no limit, then we return a NoOp lease (which doesn't limit anything)
            if (!timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.HasValue)
            {
                return CreateUnlimitedLease();
            }

            return new LimitingAuthorizedTcpConnectionLease(subscriptionId, activeConnectionCountPerSubscriptionId, timeoutsAndLimits.MaximumActiveTcpConnectionsPerPollingSubscription.Value);
        }

        public IDisposable CreateUnlimitedLease()
        {
            return new UnlimitedAuthorizedTcpConnectionLease();
        }

        class UnlimitedAuthorizedTcpConnectionLease : IDisposable
        {
            public void Dispose()
            {
            }
        }

        class LimitingAuthorizedTcpConnectionLease : IDisposable
        {
            readonly Uri subscriptionId;
            readonly Dictionary<Uri, StrongBox<int>> activeConnectionCountPerSubscriptionId;

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

                    count.Value++;

                    //validate the new count. If this throws an exception, it'll kill the connection
                    if (count.Value > maximumAcceptedTcpConnectionsPerThumbprint)
                    {
                        //decrement as this connection has been rejected
                        count.Value--;

                        //throw an exception, bailing on the connection
                        throw new ActiveTcpConnectionsExceededException(this.subscriptionId, $"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of active TCP connections for subscription {subscriptionId}");
                    }
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