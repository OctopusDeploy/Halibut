// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Halibut.Diagnostics;
using Halibut.Exceptions;

namespace Halibut.Transport
{
    public interface IActiveTcpConnectionsLimiter
    {
        IDisposable ClaimAuthorizedTcpConnection(Uri subscriptionId);

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

        public IDisposable ClaimAuthorizedTcpConnection(Uri subscriptionId)
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
                        throw new AuthorizedTcpConnectionsExceededException($"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of authorised TCP connections for thumbprint {subscriptionId}");
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