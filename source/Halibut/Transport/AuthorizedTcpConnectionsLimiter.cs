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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Util;

namespace Halibut.Transport
{
    public interface IAuthorizedTcpConnectionsLimiter
    {
        IDisposable ClaimAuthorizedTcpConnection(string thumbprint);
    }

    public class AuthorizedTcpConnectionsLimiter : IAuthorizedTcpConnectionsLimiter
    {
        readonly HalibutTimeoutsAndLimits timeoutsAndLimits;

        //we use a StrongBox here so we can use Interlock to increment/decrement the value
        ConcurrentDictionary<string, ConnectionCount> authorizedConnectionCountPerThumbprint = new();

        public AuthorizedTcpConnectionsLimiter(HalibutTimeoutsAndLimits timeoutsAndLimits)
        {
            this.timeoutsAndLimits = timeoutsAndLimits;
        }

        public IDisposable ClaimAuthorizedTcpConnection(string thumbprint)
        {
            return new AuthorizedTcpConnectionLease(thumbprint, authorizedConnectionCountPerThumbprint, timeoutsAndLimits.MaximumAuthorisedTcpConnectionsPerThumbprint);
        }

        class AuthorizedTcpConnectionLease : IDisposable
        {
            readonly string thumbprint;
            readonly ConcurrentDictionary<string, ConnectionCount> authorizedConnectionCountPerThumbprint;

            public AuthorizedTcpConnectionLease(string thumbprint, ConcurrentDictionary<string, ConnectionCount> authorizedConnectionCountPerThumbprint, int maximumAcceptedTcpConnectionsPerThumbprint)
            {
                this.thumbprint = thumbprint;
                this.authorizedConnectionCountPerThumbprint = authorizedConnectionCountPerThumbprint;

                var count = this.authorizedConnectionCountPerThumbprint.GetOrAdd(thumbprint, _ => new ConnectionCount(0));

                //increment the count via an interlock
                var resultCount = Interlocked.Increment(ref count.Count);

                //validate the new count. If this throws an exception, it'll kill the connection
                if (resultCount > maximumAcceptedTcpConnectionsPerThumbprint)
                {
                    //decrement as this connection has been rejected
                    Interlocked.Decrement(ref count.Count);

                    //throw an exception, bailing on the connection
                    throw new AuthorizedTcpConnectionsExceededException($"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of authorised TCP connections for thumbprint {thumbprint}");
                }
            }

            public void Dispose()
            {
                if (authorizedConnectionCountPerThumbprint.TryGetValue(thumbprint, out var count))
                {
                    //decrement the count of authorized connections
                    var result = Interlocked.Decrement(ref count.Count);

                    // Remove the key from the dictionary if the value is 0
                    if (result == 0)
                    {
                        // this will only remove the item if there is a value that matches this key & value
                        // so if it's been incremented after the Decrement, then this will not find a match and not remove anything
                        authorizedConnectionCountPerThumbprint.TryRemove(new KeyValuePair<string, ConnectionCount>(thumbprint, new ConnectionCount(0)));
                    }
                }
            }
        }

        class ConnectionCount : IEquatable<ConnectionCount>
        {
            public int Count;

            public ConnectionCount(int count)
            {
                Count = count;
            }

            public bool Equals(ConnectionCount? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Count == other.Count;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ConnectionCount)obj);
            }

            public override int GetHashCode()
            {
                return Count.GetHashCode();
            }
        }
    }
}