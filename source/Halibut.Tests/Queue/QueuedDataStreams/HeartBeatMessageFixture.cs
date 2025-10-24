using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.MessageStorage;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Halibut.Tests.Queue.QueuedDataStreams
{
    public class HeartBeatMessageFixture : BaseTest
    {
        [Test]
        public void Deserialize_WithValidEmptyJson_ShouldReturnHeartBeatMessageWithEmptyProgress()
        {
            // Arrange
            var json = "{\"DataStreamProgress\":{}}";

            // Act
            var heartBeatMessage = HeartBeatMessage.Deserialize(json);

            // Assert
            heartBeatMessage.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().BeEmpty();
        }

        [Test]
        public void WhenGivenAPreviouslySerialisedHeartBeatMessage_Deserialize_ShouldReturnHeartBeatMessage()
        {
            // Arrange
            var json = @"{
  ""DataStreamProgress"": {
    ""731aba31-0272-4111-80b2-8f727ef70af1"": 0,
    ""0103c541-a7b6-4590-84c2-f7098816b617"": 1024
  }
}";

            // Act
            var heartBeatMessage = HeartBeatMessage.Deserialize(json);

            // Assert
            heartBeatMessage.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().HaveCount(2);
            heartBeatMessage.DataStreamProgress.Should().ContainKey(Guid.Parse("731aba31-0272-4111-80b2-8f727ef70af1"));
            heartBeatMessage.DataStreamProgress[Guid.Parse("731aba31-0272-4111-80b2-8f727ef70af1")].Should().Be(0);
            heartBeatMessage.DataStreamProgress.Should().ContainKey(Guid.Parse("0103c541-a7b6-4590-84c2-f7098816b617"));
            heartBeatMessage.DataStreamProgress[Guid.Parse("0103c541-a7b6-4590-84c2-f7098816b617")].Should().Be(1024);
        }
        
        [Test]
        public void Deserialize_WithEmptyStringJson_ShouldReturnEmptyHeartBeatMessage()
        {
            // Act
            var heartBeatMessage = HeartBeatMessage.Deserialize(string.Empty);

            // Assert
            heartBeatMessage.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().NotBeNull();
            heartBeatMessage.DataStreamProgress.Should().BeEmpty();
        }

        [Test]
        public void Deserialize_WithEmptyDataShouldWork()
        {
            // Arrange
            var emptyJson = "{}";

            // Act & Assert
            var heartBeatMessage = HeartBeatMessage.Deserialize(emptyJson);
            heartBeatMessage.DataStreamProgress.Should().BeEmpty();
        }

        [Test]
        public void SerializeAndDeserialize_RoundTrip_ShouldWork()
        {
            // Arrange
            var dataStreamId1 = Guid.NewGuid();
            var dataStreamId2 = Guid.NewGuid();
            
            var original = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long>
                {
                    { dataStreamId1, 0L },
                    { dataStreamId2, 1024L }
                }
            };

            // Act
            var json = HeartBeatMessage.Serialize(original);
            var deserialized = HeartBeatMessage.Deserialize(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.DataStreamProgress.Should().NotBeNull();
            deserialized.DataStreamProgress.Should().HaveCount(2);
            
            deserialized.DataStreamProgress.Should().ContainKey(dataStreamId1);
            deserialized.DataStreamProgress[dataStreamId1].Should().Be(0L);
            
            deserialized.DataStreamProgress.Should().ContainKey(dataStreamId2);
            deserialized.DataStreamProgress[dataStreamId2].Should().Be(1024L);
        }
    }
}

