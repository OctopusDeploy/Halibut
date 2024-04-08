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
    public class ActiveTcpConnectionsLimiterFixture : BaseTest
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
            limiter.LeaseActiveTcpConnection(new Uri("poll://abc"));
            limiter.LeaseActiveTcpConnection(new Uri("poll://abc"));
            limiter.LeaseActiveTcpConnection(new Uri("poll://abc"));

            //this should throw
            Action x = () => limiter.LeaseActiveTcpConnection(new Uri("poll://abc"));

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
            limiter.LeaseActiveTcpConnection(subscription);
            limiter.LeaseActiveTcpConnection(subscription);

            //this will decrement the current count in the dispose
            using (limiter.LeaseActiveTcpConnection(subscription))
            {
            }

            ;

            //this should not throw
            Action x = () => limiter.LeaseActiveTcpConnection(subscription);

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
            limiter.LeaseActiveTcpConnection(subscription1);
            limiter.LeaseActiveTcpConnection(subscription1);
            limiter.LeaseActiveTcpConnection(subscription1);

            //this should not throw
            Action x = () => limiter.LeaseActiveTcpConnection(subscription2);

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
                tasks.Add(Task.Run(() =>
                {
                    //we do an extra bunch of work here to validate that the multi-threading is working
                    for (var j = 0; j < 100; j++)
                    {
                        try
                        {
                            limiter.LeaseActiveTcpConnection(subscription);
                        }
                        catch (ActiveTcpConnectionsExceededException)
                        {
                            Interlocked.Increment(ref failures);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            failures.Should().Be(1990); // 20 x 100 - limit of 10
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
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (limiter.LeaseActiveTcpConnection(subscription))
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
                        using (limiter.LeaseActiveTcpConnection(subscription))
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
                        using (limiter.LeaseActiveTcpConnection(subscription))
                        {
                        }
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