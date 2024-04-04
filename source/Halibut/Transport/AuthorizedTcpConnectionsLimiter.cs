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
using System.Runtime.CompilerServices;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Exceptions;

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
        ConcurrentDictionary<string, StrongBox<int>> authorizedConnectionCountPerThumbprint = new();

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
            readonly ConcurrentDictionary<string, StrongBox<int>> authorizedConnectionCountPerThumbprint;

            public AuthorizedTcpConnectionLease(string thumbprint, ConcurrentDictionary<string, StrongBox<int>> authorizedConnectionCountPerThumbprint, int maximumAcceptedTcpConnectionsPerThumbprint)
            {
                this.thumbprint = thumbprint;
                this.authorizedConnectionCountPerThumbprint = authorizedConnectionCountPerThumbprint;

                var count = this.authorizedConnectionCountPerThumbprint.GetOrAdd(thumbprint, _ => new StrongBox<int>(0));

                //increment the count via an interlock
                var resultCount = Interlocked.Increment(ref count.Value);

                //validate the new count. If this throws an exception, it'll
                if (resultCount > maximumAcceptedTcpConnectionsPerThumbprint)
                {
                    //decrement as this connection has been rejected
                    Interlocked.Decrement(ref count.Value);
                    throw new AuthorizedTcpConnectionsExceededException($"Exceeded the maximum number ({maximumAcceptedTcpConnectionsPerThumbprint}) of authorised TCP connections for thumbprint {thumbprint}");
                }
            }

            public void Dispose()
            {
                if (authorizedConnectionCountPerThumbprint.TryGetValue(thumbprint, out var count))
                {
                    //decrement the count of authorized connections
                    var result = Interlocked.Decrement(ref count.Value);

                    //if this was the last connection, remove the value from the dictionary
                    if (result == 0)
                    {
                        authorizedConnectionCountPerThumbprint.TryRemove(thumbprint, out _);
                    }
                }
            }
        }
    }
}