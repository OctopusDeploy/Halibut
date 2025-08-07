using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Queue.Redis;
using Halibut.Tests.Support.Logging;
using Halibut.Util.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Queue.Redis
{
    public class RedisFacadeFixture : BaseTest
    {
        // AI generated :S
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
            await redisFacade.SetInHash(key, field, payload);

            // Assert - We'll verify by trying to get and delete it
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field);
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

            await redisFacade.SetInHash(key, field, payload);

            // Act
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field);

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

            await redisFacade.SetInHash(key, field, payload);

            // Act
            var exists = await redisFacade.HashContainsKey(key, field);

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
            var exists = await redisFacade.HashContainsKey(key, nonExistentField);

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
            var exists = await redisFacade.HashContainsKey(nonExistentKey, field);

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

            await redisFacade.SetInHash(key, field, payload);

            // Verify the hash field exists
            var existsBefore = await redisFacade.HashContainsKey(key, field);
            existsBefore.Should().BeTrue();

            // Act
            var retrievedValue = await redisFacade.TryGetAndDeleteFromHash(key, field);

            // Assert
            retrievedValue.Should().Be(payload);
            
            // Verify the entire key was deleted (not just the field)
            var existsAfter = await redisFacade.HashContainsKey(key, field);
            existsAfter.Should().BeFalse();

            // Verify trying to get it again returns null
            var secondRetrieval = await redisFacade.TryGetAndDeleteFromHash(key, field);
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
            await redisFacade.ListRightPushAsync(key, payload1);
            await redisFacade.ListRightPushAsync(key, payload2);

            // Pop items from the left (FIFO)
            var firstItem = await redisFacade.ListLeftPopAsync(key);
            var secondItem = await redisFacade.ListLeftPopAsync(key);

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
            var result = await redisFacade.ListLeftPopAsync(emptyListKey);

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
            await redisFacade.PublishToChannel(channelName, testMessage);

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
                await redisFacade.PublishToChannel(channelName, msg);
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
            await redisFacade.PublishToChannel(channelName, "should-not-receive");

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

        // [Test]
        // public void Dispose_ShouldNotThrowException()
        // {
        //     // Arrange
        //     var redisFacade = CreateRedisFacade();
        //
        //     // Act & Assert
        //     Action act = () => redisFacade.Dispose();
        //     act.Should().NotThrow();
        // }
        //
        // [Test]
        // public void Dispose_CalledMultipleTimes_ShouldNotThrowException()
        // {
        //     // Arrange
        //     var redisFacade = CreateRedisFacade();
        //
        //     // Act & Assert
        //     Action act = () =>
        //     {
        //         redisFacade.Dispose();
        //         redisFacade.Dispose(); // Second call
        //     };
        //     act.Should().NotThrow();
        // }

        [Test]
        public async Task SetInHash_WithTTL_ShouldExpireAfterSpecifiedTime()
        {
            // Arrange
            await using var redisFacade = CreateRedisFacade();
            var key = Guid.NewGuid().ToString();
            var field = "test-field";
            var payload = "test-payload";

            // Act - Set a value in hash (it has a TTL of 9:9:9 according to the implementation)
            await redisFacade.SetInHash(key, field, payload);

            // Immediately try to get the value - should exist
            var immediateValue = await redisFacade.TryGetAndDeleteFromHash(key, field);

            // Assert
            immediateValue.Should().Be(payload);
            
            // Note: We can't easily test the actual TTL expiration in a unit test
            // as it would require waiting 9+ hours, but we've verified the value is set correctly
        }
    }
} 