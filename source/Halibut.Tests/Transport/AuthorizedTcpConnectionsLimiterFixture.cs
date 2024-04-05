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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    [TestFixture]
    public class AuthorizedTcpConnectionsLimiterFixture : BaseTest
    {
        [Test]
        public void LimitsConcurrentConnectionsForSingleThumbprint()
        {
            // Arrange
            const int limit = 3;
            const string thumbprint = "tp-1";
            var limiter = new AuthorizedTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumAuthorisedTcpConnectionsPerThumbprint = limit
            });

            // Act
            limiter.ClaimAuthorizedTcpConnection(thumbprint);
            limiter.ClaimAuthorizedTcpConnection(thumbprint);
            limiter.ClaimAuthorizedTcpConnection(thumbprint);

            //this should throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(thumbprint);

            // Assert
            x.Should().Throw<AuthorizedTcpConnectionsExceededException>();
        }
        
        [Test]
        public void CompletedLeasesAreRemovedFromTheCount()
        {
            // Arrange
            const int limit = 3;
            const string thumbprint = "tp-1";
            var limiter = new AuthorizedTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumAuthorisedTcpConnectionsPerThumbprint = limit
            });

            // Act
            limiter.ClaimAuthorizedTcpConnection(thumbprint);
            limiter.ClaimAuthorizedTcpConnection(thumbprint);
            
            //this will decrement the current count in the dispose
            using ( limiter.ClaimAuthorizedTcpConnection(thumbprint))
            {
            };

            //this should not throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(thumbprint);

            // Assert
            x.Should().NotThrow<AuthorizedTcpConnectionsExceededException>();
        }
        
        [Test]
        public void DoesNotLimitConcurrentConnectionsForDifferentThumbprints()
        {
            // Arrange
            const int limit = 3;
            const string thumbprint1 = "tp-1";
            const string thumbprint2 = "tp-2";
            var limiter = new AuthorizedTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumAuthorisedTcpConnectionsPerThumbprint = limit
            });

            // Act
            limiter.ClaimAuthorizedTcpConnection(thumbprint1);
            limiter.ClaimAuthorizedTcpConnection(thumbprint1);
            limiter.ClaimAuthorizedTcpConnection(thumbprint1);

            //this should not throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(thumbprint2);

            // Assert
            x.Should().NotThrow<AuthorizedTcpConnectionsExceededException>();
        }
        
        [Test]
        public async Task ShouldHandleMultiThreading()
        {
            // Arrange
            const int limit = 10;
            const string thumbprint = "tp-1";
            var limiter = new AuthorizedTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumAuthorisedTcpConnectionsPerThumbprint = limit
            });
            
            // Capture how many claims fail with the exception
            var failures = 0;

            // Act
            var tasks = new List<Task>();
            for (var i = 0; i < 20; i++)
            {
                var x = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        limiter.ClaimAuthorizedTcpConnection(thumbprint);
                    }
                    catch (AuthorizedTcpConnectionsExceededException)
                    {
                        var count = Interlocked.Increment(ref failures);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            failures.Should().Be(10);
        }
        
        [Test]
        public async Task ShouldHandleMultiThreadingWithFakeWorkDuringLease()
        {
            // Arrange
            const int limit = 25;
            const string thumbprint = "tp-1";
            var limiter = new AuthorizedTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumAuthorisedTcpConnectionsPerThumbprint = limit
            });
            
            // Capture how many claims fail with the exception
            var failures = 0;

            // Act
            var tasks = new List<Task>();
            
            //We spawn 25 with a 1s delay in the work
            //these will claim all the available leases
            for (var i = 0; i < 25; i++)
            {
                var x = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (limiter.ClaimAuthorizedTcpConnection(thumbprint))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                    catch (AuthorizedTcpConnectionsExceededException)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }));
            }
            
            //now we claim another 20, which should all fail
            for (var i = 0; i < 20; i++)
            {
                var x = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (limiter.ClaimAuthorizedTcpConnection(thumbprint))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                    catch (AuthorizedTcpConnectionsExceededException)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }));
            }

            //wait for everything to complete
            await Task.WhenAll(tasks);
            tasks.Clear();
            
            //try another 20 which should all succeed
            for (var i = 0; i < 20; i++)
            {
                var x = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        using (limiter.ClaimAuthorizedTcpConnection(thumbprint))
                        { }
                    }
                    catch (AuthorizedTcpConnectionsExceededException)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            failures.Should().Be(20);
        }
    }
}