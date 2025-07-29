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
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Newtonsoft.Json;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Halibut.Queue.Redis
{
    public class RedisFacade : IDisposable
    {
        readonly Lazy<ConnectionMultiplexer> connection;

        ConnectionMultiplexer Connection => connection.Value;

        public RedisFacade(string redisHost)
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

        public async Task<IAsyncDisposable> SubscribeToChannel(string channelName, Func<ChannelMessage, Task> onMessage)
        {
            channelName = "channel:" + channelName;
            // TODO ever call needs to respect the cancellation token
            var channel = await Connection.GetSubscriber()
                .SubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal));
            
            channel.OnMessage(onMessage);
            
            return new FuncAsyncDisposable(() => channel.UnsubscribeAsync());
        }
        
        public async Task PublishToChannel(string channelName, string payload)
        {
            channelName = "channel:" + channelName;
            var subscriber = Connection.GetSubscriber();
            await subscriber.PublishAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal), payload);
        }
        
        public async Task SetInHash(string key, string field, string payload)
        {
            key = "hash:" + key;
            // TODO: TTL
            // TODO ever call needs to respect the cancellation token
            var ttl = new TimeSpan(9, 9, 9);
            var database = Connection.GetDatabase();
            await database.HashSetAsync(key, new RedisValue(field), new RedisValue(payload));
            await database.KeyExpireAsync(key, ttl);
        }

        public async Task<string?> TryGetAndDeleteFromHash(string key, string field)
        {
            // TODO ever call needs to respect the cancellation token
            key = "hash:" + key;
            var database = Connection.GetDatabase();
            var value = await database.HashGetAsync(key, new RedisValue(field));
            var res = await database.KeyDeleteAsync(key);
            if (!res)
            {
                // Someone else deleted this, so return nothing to make the get and delete appear to be atomic. 
                return null;
            } 
            return value;
        }

        public async Task ListRightPushAsync(string key, string payload)
        {
            key = "list:" + key;
            var database = Connection.GetDatabase();
            // TODO can we set TTL on this?
            await database.ListRightPushAsync(key, payload);
        }

        public async Task<string?> ListLeftPopAsync(string key)
        {
            key = "list:" + key;
            var database = Connection.GetDatabase();
            var value = await database.ListLeftPopAsync(key);
            if (value.IsNull)
            {
                return null;
            }

            return value;
        }

        public async Task SetString(string key, string value)
        {
            key = "string:" + key;
            var database = Connection.GetDatabase();
            await database.StringSetAsync(key, value);
        }
        
        public async Task<string?> GetString(string key)
        {
            key = "string:" + key;
            var database = Connection.GetDatabase();
            return await database.StringGetAsync(key);
        }
    }

}