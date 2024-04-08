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
        public void LimitsConcurrentConnectionsForSingleSubscription()
        {
            // Arrange
            const int limit = 3;
            var limiter = new ActiveTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumActiveTcpConnectionsPerPollingSubscription = limit
            });

            // Act
            //we create a new URI each time to make sure we aren't doing object reference checks
            limiter.ClaimAuthorizedTcpConnection(new Uri("poll://abc"));
            limiter.ClaimAuthorizedTcpConnection(new Uri("poll://abc"));
            limiter.ClaimAuthorizedTcpConnection(new Uri("poll://abc"));

            //this should throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(new Uri("poll://abc"));

            // Assert
            x.Should().Throw<ActiveTcpConnectionsExceededException>();
        }
        
        [Test]
        public void CompletedLeasesAreRemovedFromTheCount()
        {
            // Arrange
            const int limit = 3;
            var subscription = new Uri("poll://abc");
            var limiter = new ActiveTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumActiveTcpConnectionsPerPollingSubscription = limit
            });

            // Act
            limiter.ClaimAuthorizedTcpConnection(subscription);
            limiter.ClaimAuthorizedTcpConnection(subscription);
            
            //this will decrement the current count in the dispose
            using ( limiter.ClaimAuthorizedTcpConnection(subscription))
            {
            };

            //this should not throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(subscription);

            // Assert
            x.Should().NotThrow<ActiveTcpConnectionsExceededException>();
        }
        
        [Test]
        public void DoesNotLimitConcurrentConnectionsForDifferentSubscriptions()
        {
            // Arrange
            const int limit = 3;
            var subscription1 = new Uri("poll://abc");
            var subscription2 = new Uri("poll://dev");
            var limiter = new ActiveTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumActiveTcpConnectionsPerPollingSubscription = limit
            });

            // Act
            limiter.ClaimAuthorizedTcpConnection(subscription1);
            limiter.ClaimAuthorizedTcpConnection(subscription1);
            limiter.ClaimAuthorizedTcpConnection(subscription1);

            //this should not throw
            Action x = () => limiter.ClaimAuthorizedTcpConnection(subscription2);

            // Assert
            x.Should().NotThrow<ActiveTcpConnectionsExceededException>();
        }
        
        [Test]
        public async Task ShouldHandleMultiThreading()
        {
            // Arrange
            const int limit = 10;
            var subscription = new Uri("poll://abc");
            var limiter = new ActiveTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumActiveTcpConnectionsPerPollingSubscription = limit
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
                        limiter.ClaimAuthorizedTcpConnection(subscription);
                    }
                    catch (ActiveTcpConnectionsExceededException)
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
            var subscription = new Uri("poll://abc");
            var limiter = new ActiveTcpConnectionsLimiter(new HalibutTimeoutsAndLimits
            {
                MaximumActiveTcpConnectionsPerPollingSubscription = limit
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
                        using (limiter.ClaimAuthorizedTcpConnection(subscription))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                    catch (ActiveTcpConnectionsExceededException)
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
                        using (limiter.ClaimAuthorizedTcpConnection(subscription))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                    catch (ActiveTcpConnectionsExceededException)
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
                        using (limiter.ClaimAuthorizedTcpConnection(subscription))
                        { }
                    }
                    catch (ActiveTcpConnectionsExceededException)
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