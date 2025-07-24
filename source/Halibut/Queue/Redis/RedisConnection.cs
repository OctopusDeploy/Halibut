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
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Halibut.Queue.Redis
{
    class RedisConnection : IRedisConnection, IDisposable
    {
        readonly Lazy<ConnectionMultiplexer> connection;

        ConnectionMultiplexer Connection => connection.Value;

        public RedisConnection(string redisHost)
        {

            connection = new Lazy<ConnectionMultiplexer>(() =>
            {
                    return ConnectionMultiplexer.Connect(redisHost);
            });
        }

        public void Dispose()
        {
            if (connection.IsValueCreated)
            {
                connection.Value.Dispose();
            }
        }

        public void SubscribeToChannel(string channel, Action<ChannelMessage> onMessage)
        {
            Connection.GetSubscriber()
                .Subscribe(channel)
                .OnMessage(onMessage);
        }

        public async Task PublishToChannel<T>(string channel, T payload)
        {
            var serialized = JsonConvert.SerializeObject(payload);
            await PublishToChannel(channel, serialized);

        }

        public async Task PublishToChannel(string channel, string payload)
        {
            var subscriber = Connection.GetSubscriber();
            await subscriber.PublishAsync(channel, payload);
        }

        public async Task StringSet(string key, string payload, TimeSpan expiry)
        {
            var database = Connection.GetDatabase();
            await database.StringSetAsync(key, payload, expiry:expiry);
        }

        public async Task ListRightPush(string key, string payload)
        {
            var database = Connection.GetDatabase();
            await database.ListRightPushAsync(key, payload);
        }

        public async Task<string?> StringGetDelete(string key)
        {
            var database = Connection.GetDatabase();
            var payload = await database.StringGetDeleteAsync(key);
            return payload;
        }

        public async Task<string?> ListLeftPopAsync(string key)
        {
            var database = Connection.GetDatabase();
            var value = await database.ListLeftPopAsync(key);
            if (value.IsNull)
            {
                return null;
            }

            return value;
        }
    }

    public interface IRedisConnection
    {
        void SubscribeToChannel(string channel, Action<ChannelMessage> onMessage);

        Task PublishToChannel(string channel, string payload);
        Task PublishToChannel<T>(string channel, T payload);

        Task StringSet(string key, string payload, TimeSpan expiry);
        Task ListRightPush(string key, string payload);
        Task<string?> StringGetDelete(string key);
        
        Task<string?> ListLeftPopAsync(string key);
    }
}