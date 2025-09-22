#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using NUnit.Framework;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis.RedisHelpers
{
    [RedisTest]
    public class RedisFacadeObserverTest : BaseTest
    {
        [Test]
        public async Task WhenRedisConnectionGoesDown_ObserverShouldBeNotifiedOfRetryExceptions()
        {
            // Arrange
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            var testObserver = new TestRedisFacadeObserver();

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, redisFacadeObserver: testObserver);

            // Verify Redis is working initially
            await redisFacade.SetString("foo", "bar", TimeSpan.FromMinutes(1), CancellationToken);
            (await redisFacade.GetString("foo", CancellationToken)).Should().Be("bar");

            // Kill Redis connections to simulate network issues
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // This should trigger retries and call the observer
            var getStringTask = redisFacade.GetString("foo", CancellationToken);
            
            // Wait a bit for retries to happen, then restore connection
            await Task.Delay(6000); // By-default (somewhere) the redis client will wait 5s for a request to get to Redis so we need to wait longer than that.
            portForwarder.ReturnToNormalMode();

            // The operation should eventually succeed
            var result = await getStringTask;
            result.Should().Be("bar");

            // Verify that the observer was called with retry exceptions
            testObserver.ExecuteWithRetryExceptions.Should().NotBeEmpty("Observer should have been called for retry exceptions during connection issues");
            testObserver.ExecuteWithRetryExceptions.Should().AllSatisfy(ex => 
            {
                ex.Exception.Should().NotBeNull("Exception should not be null");
                ex.WillRetry.Should().BeTrue();
            });

            testObserver.ConnectionRestorations.Count.Should().BeGreaterThanOrEqualTo(1);
            testObserver.ConnectionFailures.Count.Should().BeGreaterThanOrEqualTo(1);
        }
        
        [Test]
        public async Task WhenRedisConnectionGoesDown_AndStaysDown_ObserverShouldBeNotifiedOfRetryExceptions()
        {
            // Arrange
            using var portForwarder = PortForwardingToRedisBuilder.ForwardingToRedis(Logger);
            var testObserver = new TestRedisFacadeObserver();

            await using var redisFacade = RedisFacadeBuilder.CreateRedisFacade(portForwarder, redisFacadeObserver: testObserver);
            redisFacade.MaxDurationToRetryFor = TimeSpan.FromSeconds(1);

            // Verify Redis is working initially
            await redisFacade.SetString("foo", "bar", TimeSpan.FromMinutes(1), CancellationToken);
            (await redisFacade.GetString("foo", CancellationToken)).Should().Be("bar");

            // Kill Redis connections to simulate network issues
            portForwarder.EnterKillNewAndExistingConnectionsMode();
            
            // This should trigger retries and call the observer, then ultimately fail
            var exception = await AssertThrowsAny.Exception(async () => 
                await redisFacade.GetString("foo", CancellationToken));
            
            exception.Should().NotBeNull("Operation should fail when Redis stays down");

            // Verify that the observer was called with retry exceptions
            testObserver.ExecuteWithRetryExceptions.Should().NotBeEmpty("Observer should have been called for retry exceptions during connection issues");
            testObserver.ExecuteWithRetryExceptions.Should().AllSatisfy(ex => 
            {
                ex.Exception.Should().NotBeNull("Exception should not be null");
                ex.WillRetry.Should().BeFalse();
            });

            testObserver.ConnectionRestorations.Count.Should().Be(0);
            testObserver.ConnectionFailures.Count.Should().BeGreaterThanOrEqualTo(1);
        }

        class TestRedisFacadeObserver : IRedisFacadeObserver
        {
            public List<(string? EndPoint, ConnectionFailureType FailureType, Exception? Exception)> ConnectionFailures { get; } = new();
            public List<(string? EndPoint, string Message)> ErrorMessages { get; } = new();
            public List<string?> ConnectionRestorations { get; } = new();
            public List<(Exception Exception, bool WillRetry)> ExecuteWithRetryExceptions { get; } = new();

            public void OnRedisConnectionFailed(string? endPoint, ConnectionFailureType failureType, Exception? exception)
            {
                ConnectionFailures.Add((endPoint, failureType, exception));
            }

            public void OnRedisServerRepliedWithAnErrorMessage(string? endPoint, string message)
            {
                ErrorMessages.Add((endPoint, message));
            }

            public void OnRedisConnectionRestored(string? endPoint)
            {
                ConnectionRestorations.Add(endPoint);
            }

            public void OnRedisOperationFailed(Exception exception, bool willRetry)
            {
                ExecuteWithRetryExceptions.Add((exception, willRetry));
            }
        }
    }
}
#endif
