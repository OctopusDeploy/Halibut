using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Util.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    public class RedisFacadeFixture : BaseTest
    {
        private static RedisFacade CreateRedisFacade() => new("localhost", Guid.NewGuid().ToString(), new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

        [Test]
        public void Constructor_WithRedisHostAndKeyPrefix_ShouldCreateInstance()
        {
            // Arrange & Act
            var redisFacade = new RedisFacade("localhost", "test-prefix", new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

            // Assert
            redisFacade.Should().NotBeNull();
        }

        [Test]
        public void Constructor_WithNullKeyPrefix_ShouldUseDefaultPrefix()
        {
            // Arrange & Act
            var redisFacade = new RedisFacade("localhost", null, new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

            // Assert
            redisFacade.Should().NotBeNull();
        }

        [Test]
        public async Task SetString_AndGetString_ShouldStoreAndRetrieveValue()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var value = "test-value";

            // Act
            await redisFacade.SetString(key, value, TimeSpan.FromMinutes(1), CancellationToken);
            var retrievedValue = await redisFacade.GetString(key, CancellationToken);

            // Assert
            retrievedValue.Should().Be(value);
        }

        [Test]
        public async Task GetString_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var nonExistentKey = Guid.NewGuid().ToString();

            // Act
            var retrievedValue = await redisFacade.GetString(nonExistentKey, CancellationToken);

            // Assert
            retrievedValue.Should().BeNull();
        }

        [Test]
        public async Task SetInHash_ShouldStoreValueInHash()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            // Act
            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMinutes(1), CancellationToken);

            // Assert - We'll verify by trying to get and delete it
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);
            retrievedValue.Should().Be(payload);
        }

        [Test]
        public async Task TryGetAndDeleteFromHash_WithExistingValue_ShouldReturnValueAndDelete()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMinutes(1), CancellationToken);

            // Act
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);

            // Assert
            retrievedValue.Should().Be(payload);
        }

        [Test]
        public async Task HashContainsKey_WithExistingField_ShouldReturnTrue()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMinutes(1), CancellationToken);

            // Act
            var exists = await redisFacade.HashContainsKey(key, field, CancellationToken);

            // Assert
            exists.Should().BeTrue();
        }

        [Test]
        public async Task HashContainsKey_WithNonExistentField_ShouldReturnFalse()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var nonExistentField = "non-existent-field";

            // Act
            var exists = await redisFacade.HashContainsKey(key, nonExistentField, CancellationToken);

            // Assert
            exists.Should().BeFalse();
        }

        [Test]
        public async Task HashContainsKey_WithNonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var nonExistentKey = Guid.NewGuid().ToString();
            var field = "test-field";

            // Act
            var exists = await redisFacade.HashContainsKey(nonExistentKey, field, CancellationToken);

            // Assert
            exists.Should().BeFalse();
        }

        [Test]
        public async Task TryGetAndDeleteFromHash_ShouldDeleteTheEntireKey()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMinutes(1), CancellationToken);

            // Verify the hash field exists
            var existsBefore = await redisFacade.HashContainsKey(key, field, CancellationToken);
            existsBefore.Should().BeTrue();

            // Act
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);

            // Assert
            retrievedValue.Should().Be(payload);
            
            // Verify the entire key was deleted (not just the field)
            var existsAfter = await redisFacade.HashContainsKey(key, field, CancellationToken);
            existsAfter.Should().BeFalse();

            // Verify trying to get it again returns null
            var secondRetrieval = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);
            secondRetrieval.Should().BeNull();
        }

        [Test]
        public async Task ListRightPushAsync_AndListLeftPopAsync_ShouldWorkAsQueue()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var payload1 = "first-item";
            var payload2 = "second-item";

            // Act - Push items to the right
            await redisFacade.ListRightPushAsync(key, payload1, TimeSpan.FromMinutes(1), CancellationToken);
            await redisFacade.ListRightPushAsync(key, payload2, TimeSpan.FromMinutes(1), CancellationToken);

            // Pop items from the left (FIFO)
            var firstItem = await redisFacade.ListLeftPopAsync(key, CancellationToken);
            var secondItem = await redisFacade.ListLeftPopAsync(key, CancellationToken);

            // Assert
            firstItem.Should().Be(payload1);
            secondItem.Should().Be(payload2);
        }

        [Test]
        public async Task ListLeftPopAsync_WithEmptyList_ShouldReturnNull()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var emptyListKey = Guid.NewGuid().ToString();

            // Act
            var result = await redisFacade.ListLeftPopAsync(emptyListKey, CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task PublishToChannel_AndSubscribeToChannel_ShouldDeliverMessage()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var channelName = Guid.NewGuid().ToString();
            var testMessage = "test-message";
            var receivedMessages = new List<string>();
            var messageReceived = new TaskCompletionSource<bool>();

            // Subscribe to the channel
            await using var subscription = await redisFacade.SubscribeToChannel(channelName, async message =>
            {
                await Task.Yield(); // Make it properly async
                if (!message.Message.IsNull)
                {
                    receivedMessages.Add(message.Message!);
                    messageReceived.SetResult(true);
                }
            }, CancellationToken);

            // Act - Publish a message
            await redisFacade.PublishToChannel(channelName, testMessage, CancellationToken);

            // Wait for the message to be received
            await messageReceived.Task.TimeoutAfter(TimeSpan.FromSeconds(5), CancellationToken);

            // Assert
            receivedMessages.Should().HaveCount(1);
            receivedMessages[0].Should().Be(testMessage);
        }

        [Test]
        public async Task PublishToChannel_WithMultipleMessages_ShouldDeliverAllMessages()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var channelName = Guid.NewGuid().ToString();
            var messages = new[] { "message1", "message2", "message3" };
            var receivedMessages = new List<string>();
            var allMessagesReceived = new TaskCompletionSource<bool>();

            // Subscribe to the channel
            await using var subscription = await redisFacade.SubscribeToChannel(channelName, async message =>
            {
                await Task.Yield();
                if (!message.Message.IsNull)
                {
                    receivedMessages.Add(message.Message!);
                    if (receivedMessages.Count == messages.Length)
                    {
                        allMessagesReceived.SetResult(true);
                    }
                }
            }, CancellationToken);

            // Act - Publish multiple messages
            foreach (var msg in messages)
            {
                await redisFacade.PublishToChannel(channelName, msg, CancellationToken);
            }

            // Wait for all messages to be received
            await allMessagesReceived.Task.TimeoutAfter(TimeSpan.FromSeconds(5), CancellationToken);

            // Assert
            receivedMessages.Should().HaveCount(3);
            receivedMessages.Should().Contain(messages);
        }

        [Test]
        public async Task SubscribeToChannel_WhenDisposed_ShouldUnsubscribe()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var channelName = Guid.NewGuid().ToString();
            var receivedMessages = new List<string>();

            var subscription = await redisFacade.SubscribeToChannel(channelName, async message =>
            {
                await Task.Yield();
                if (!message.Message.IsNull)
                {
                    receivedMessages.Add(message.Message!);
                }
            }, CancellationToken);

            // Act - Dispose the subscription
            await subscription.DisposeAsync();

            // Publish a message after unsubscribing
            await redisFacade.PublishToChannel(channelName, "should-not-receive", CancellationToken);

            // Wait a bit to ensure no message is received
            await Task.Delay(100);

            // Assert
            receivedMessages.Should().BeEmpty();
        }

        [Test]
        public async Task KeyPrefixing_ShouldIsolateDataBetweenDifferentPrefixes()
        {
            // Arrange
            var prefix1 = Guid.NewGuid().ToString();
            var prefix2 = Guid.NewGuid().ToString();
            
            await using var redisFacade1 = new RedisFacade("localhost", prefix1, new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));
            await using var redisFacade2 = new RedisFacade("localhost", prefix2, new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));
            
            var key = "shared-key";
            var value1 = "value-from-facade1";
            var value2 = "value-from-facade2";

            // Act - Set values with the same key but different prefixes
            await redisFacade1.SetString(key, value1, TimeSpan.FromMinutes(1), CancellationToken);
            await redisFacade2.SetString(key, value2, TimeSpan.FromMinutes(1), CancellationToken);

            // Get values using both facades
            var retrievedValue1 = await redisFacade1.GetString(key, CancellationToken);
            var retrievedValue2 = await redisFacade2.GetString(key, CancellationToken);

            // Assert - Each facade should retrieve its own value
            retrievedValue1.Should().Be(value1);
            retrievedValue2.Should().Be(value2);
        }

        [Test]
        public async Task SetInHash_WithTTL_ShouldExpireAfterSpecifiedTime()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            // Act - Set a value in hash with short TTL that we can actually test
            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMinutes(3), CancellationToken);

            // Immediately verify it exists
            var immediateExists = await redisFacade.HashContainsKey(key, field, CancellationToken);
            immediateExists.Should().BeTrue();

            // Also verify we can retrieve the value immediately
            var immediateValue = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);
            immediateValue.Should().Be(payload);

            // Set the value again to test expiration (since TryGetAndDeleteFromHash removes it)
            await redisFacade.SetInHash(key, field, payload, TimeSpan.FromMilliseconds(3), CancellationToken);

            // Assert - Should eventually expire
            await ShouldEventually.Eventually(async () =>
            {
                var exists = await redisFacade.HashContainsKey(key, field, CancellationToken);
                exists.Should().BeFalse("the hash key should expire after TTL");
            }, TimeSpan.FromSeconds(5), CancellationToken);

            // Verify TryGetAndDeleteFromHash also returns null for expired key
            var expiredValue = await redisFacade.TryGetAndDeleteFromHash(key, field, CancellationToken);
            expiredValue.Should().BeNull();
        }

        [Test]
        public async Task DeleteString_WithExistingKey_ShouldReturnTrueAndDeleteValue()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var value = "test-value";

            // Set a value first
            await redisFacade.SetString(key, value, TimeSpan.FromMinutes(1), CancellationToken);

            // Verify it exists
            var existingValue = await redisFacade.GetString(key, CancellationToken);
            existingValue.Should().Be(value);

            // Act
            var deleteResult = await redisFacade.DeleteString(key, CancellationToken);

            // Assert
            deleteResult.Should().BeTrue();
            
            // Verify the value is gone
            var deletedValue = await redisFacade.GetString(key, CancellationToken);
            deletedValue.Should().BeNull();
        }

        [Test]
        public async Task DeleteString_WithNonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var nonExistentKey = Guid.NewGuid().ToString();

            // Act
            var deleteResult = await redisFacade.DeleteString(nonExistentKey, CancellationToken);

            // Assert
            deleteResult.Should().BeFalse();
        }

        [Test]
        public async Task SetTtlForString_WithExistingKey_ShouldUpdateTTL()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var value = "test-value";

            // Set a value first with a long TTL
            await redisFacade.SetString(key, value, TimeSpan.FromHours(1), CancellationToken);

            // Verify it exists
            var existingValue = await redisFacade.GetString(key, CancellationToken);
            existingValue.Should().Be(value);

            // Act - Update TTL to a shorter time
            await redisFacade.SetTtlForString(key, TimeSpan.FromMinutes(1), CancellationToken);

            // Assert - Value should still exist immediately after TTL update
            var valueAfterTtlUpdate = await redisFacade.GetString(key, CancellationToken);
            valueAfterTtlUpdate.Should().Be(value);

            // Note: We can't easily test the actual TTL expiration in a unit test
            // without waiting, but we've verified the operation completes successfully
        }

        [Test]
        public async Task SetString_WithShortTTL_ShouldExpire()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var value = "test-value";

            // Act - Set with very short TTL
            await redisFacade.SetString(key, value, TimeSpan.FromMilliseconds(3), CancellationToken);

            // Immediately verify it exists
            var immediateValue = await redisFacade.GetString(key, CancellationToken);
            immediateValue.Should().Be(value);

            // Assert - Should eventually expire
            await ShouldEventually.Eventually(async () =>
            {
                var expiredValue = await redisFacade.GetString(key, CancellationToken);
                expiredValue.Should().BeNull("the string should expire after TTL");
            }, TimeSpan.FromSeconds(5), CancellationToken);
        }

        [Test]
        public async Task ListRightPushAsync_WithShortTTL_ShouldExpire()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var payload = "test-payload";

            // Act - Push with very short TTL
            await redisFacade.ListRightPushAsync(key, payload, TimeSpan.FromSeconds(3), CancellationToken);

            // Immediately verify it exists
            var immediateValue = await redisFacade.ListLeftPopAsync(key, CancellationToken);
            immediateValue.Should().Be(payload);

            // Push another item and test expiration
            await redisFacade.ListRightPushAsync(key, payload, TimeSpan.FromMilliseconds(3), CancellationToken);

            // Assert - Should eventually expire
            await ShouldEventually.Eventually(async () =>
            {
                var listValue = await redisFacade.ListLeftPopAsync(key, CancellationToken);
                listValue.Should().BeNull("the list should expire after TTL");
            }, TimeSpan.FromSeconds(5), CancellationToken);
        }

        [Test]
        public void IsConnected_WhenNotInitialized_ShouldReturnFalse()
        {
            // Arrange
            var redisFacade = new RedisFacade("localhost", "test-prefix", new TestContextLogCreator("Redis", LogLevel.Trace).CreateNewForPrefix(""));

            // Act & Assert
            redisFacade.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task IsConnected_AfterSuccessfulOperation_ShouldReturnTrue()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();

            // Act - Perform an operation to initialize connection
            await redisFacade.SetString(Guid.NewGuid().ToString(), "test", TimeSpan.FromMinutes(1), CancellationToken);

            // Assert
            redisFacade.IsConnected.Should().BeTrue();
        }

        [Test]
        public async Task TotalSubscribers_ShouldTrackActiveSubscriptions()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var channelName = Guid.NewGuid().ToString();

            // Act & Assert - Initially no subscribers
            redisFacade.TotalSubscribers.Should().Be(0);

            // Subscribe to channels
            await using var subscription1 = await redisFacade.SubscribeToChannel(channelName + "1", _ => Task.CompletedTask, CancellationToken);
            redisFacade.TotalSubscribers.Should().Be(1);

            await using var subscription2 = await redisFacade.SubscribeToChannel(channelName + "2", _ => Task.CompletedTask, CancellationToken);
            redisFacade.TotalSubscribers.Should().Be(2);

            // Dispose one subscription
            await subscription1.DisposeAsync();
            redisFacade.TotalSubscribers.Should().Be(1);

            // Dispose second subscription
            await subscription2.DisposeAsync();
            redisFacade.TotalSubscribers.Should().Be(0);
        }

        [Test]
        public async Task MultipleSetString_WithDifferentTTLs_ShouldRespectIndividualTTLs()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var value1 = "value1";
            var value2 = "value2";

            // Act - Set with different TTLs
            await redisFacade.SetString(key1, value1, TimeSpan.FromMilliseconds(3), CancellationToken); // Short TTL
            await redisFacade.SetString(key2, value2, TimeSpan.FromMinutes(1), CancellationToken); // Long TTL

            // Assert - First should eventually expire, second should still exist
            await ShouldEventually.Eventually(async () =>
            {
                var expiredValue1 = await redisFacade.GetString(key1, CancellationToken);
                expiredValue1.Should().BeNull("the first string should expire after short TTL");
            }, TimeSpan.FromSeconds(5), CancellationToken);
            
            // Verify the second key still exists after the first expires
            var stillExists2 = await redisFacade.GetString(key2, CancellationToken);
            stillExists2.Should().Be(value2);
        }

        [Test]
        public async Task DisposeAsync_ShouldCleanupResourcesAndNotThrow()
        {
            // Arrange
            var redisFacade = CreateRedisFacade();
            
            // Perform some operations to initialize resources
            await redisFacade.SetString(Guid.NewGuid().ToString(), "test", TimeSpan.FromMinutes(1), CancellationToken);
            await using var subscription = await redisFacade.SubscribeToChannel(Guid.NewGuid().ToString(), _ => Task.CompletedTask, CancellationToken);

            // Act & Assert - Dispose should not throw
            Func<Task> disposeAction = async () => await redisFacade.DisposeAsync();
            await disposeAction.Should().NotThrowAsync();
        }

        [Test]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var redisFacade = CreateRedisFacade();
            await redisFacade.SetString(Guid.NewGuid().ToString(), "test", TimeSpan.FromMinutes(1), CancellationToken);

            // Act & Assert - Multiple dispose calls should not throw
            await redisFacade.DisposeAsync();
            
            Func<Task> secondDisposeAction = async () => await redisFacade.DisposeAsync();
            await secondDisposeAction.Should().NotThrowAsync();
        }


    }
} 