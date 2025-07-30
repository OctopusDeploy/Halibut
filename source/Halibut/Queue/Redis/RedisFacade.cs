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
using Halibut.Diagnostics; // Add logging support
using Newtonsoft.Json;
using Nito.AsyncEx;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Halibut.Queue.Redis
{
    /// <summary>
    /// Facade for Redis operations with built-in connection monitoring and disconnect detection.
    /// 
    /// Usage example for connection monitoring:
    /// <code>
    /// var facade = new RedisFacade("localhost:6379", "myapp", logger);
    /// 
    /// // Monitor overall connection events
    /// facade.ConnectionFailed += message => Console.WriteLine($"Connection failed: {message}");
    /// facade.ConnectionRestored += message => Console.WriteLine($"Connection restored: {message}");
    /// facade.ErrorOccurred += message => Console.WriteLine($"Redis error: {message}");
    /// 
    /// // Subscribe with per-subscription monitoring
    /// var subscription = await facade.SubscribeToChannel("my-channel", async message => {
    ///     Console.WriteLine($"Received: {message}");
    /// });
    /// 
    /// // Monitor individual subscription disconnects
    /// if (subscription is RedisSubscriptionWrapper wrapper)
    /// {
    ///     wrapper.SubscriptionDisconnected += message => Console.WriteLine($"Subscription lost: {message}");
    ///     wrapper.SubscriptionReconnected += message => Console.WriteLine($"Subscription restored: {message}");
    /// }
    /// 
    /// // Check connection status
    /// if (!facade.IsConnected)
    /// {
    ///     Console.WriteLine("Redis is not connected!");
    /// }
    /// </code>
    /// </summary>
    public class RedisFacade : IAsyncDisposable
    {
        readonly Lazy<ConnectionMultiplexer> connection;
        readonly ILog log;

        ConnectionMultiplexer Connection => connection.Value;
        
        string keyPrefix;


        CancellationTokenSource cts;
        CancellationToken facadeCancellationToken;

        public RedisFacade(string configuration, string? keyPrefix, ILog log) : this(ConfigurationOptions.Parse(configuration), keyPrefix, log)
        {
            
        }
        public RedisFacade(ConfigurationOptions redisOptions, string? keyPrefix, ILog log)
        {
            this.keyPrefix = keyPrefix ?? "halibut";
            this.log = log;
            this.cts = new CancellationTokenSource();
            this.facadeCancellationToken = cts.Token;
            
            connection = new Lazy<ConnectionMultiplexer>(() =>
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                
                //redisOptions.ReconnectRetryPolicy = new LinearRetry()
                
                // Subscribe to connection events
                multiplexer.ConnectionFailed += OnConnectionFailed;
                multiplexer.ConnectionRestored += OnConnectionRestored;
                multiplexer.ErrorMessage += OnErrorMessage;
                
                return multiplexer;
            });
        }


        public class ConnectionInErrorHelper
        {
            readonly TaskCompletionSource connectionInError = new TaskCompletionSource();
            
            public bool IsConnectionInError => connectionInError.Task.IsCompleted;
            
            public void SetIsInError() => connectionInError.SetResult();
            
            public Task CompletesWhenAConnectionErrorOccurs => connectionInError.Task;
        }
        
        private ConnectionInErrorHelper AConnectionHasErroredOutSinceYouGotThis = new ConnectionInErrorHelper();
        private readonly object errorOccuredLock = new object();
        
        private ConnectionInErrorHelper ConnectionInErrorHelperProvider() => AConnectionHasErroredOutSinceYouGotThis;
        

        private void RecordConnectionErrorHasOccured()
        {
            lock (errorOccuredLock)
            {
                var inErrorConnectionInError = this.AConnectionHasErroredOutSinceYouGotThis;
                this.AConnectionHasErroredOutSinceYouGotThis = new ();
                inErrorConnectionInError.SetIsInError();
            }
        } 
        private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            RecordConnectionErrorHasOccured();
            
            var message = $"Redis connection failed - EndPoint: {e.EndPoint}, Failure: {e.FailureType}, Exception: {e.Exception?.Message}";
            
            log?.Write(EventType.Error, message);
        }

        private void OnErrorMessage(object? sender, RedisErrorEventArgs e)
        {
            var message = $"Redis error - EndPoint: {e.EndPoint}, Message: {e.Message}";
            log?.Write(EventType.Error, message);
        }
        
        private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            var message = $"Redis connection restored - EndPoint: {e.EndPoint}";
            log?.Write(EventType.Diagnostic, message);
        }

        public bool IsConnected => connection.IsValueCreated && Connection.IsConnected;

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await cts.CancelAsync());
            Try.IgnoringError(() => cts.Dispose());
            
            if (connection.IsValueCreated)
            {
                var conn = connection.Value;
                
                Try.IgnoringError(() =>
                {
                    // Unsubscribe from events before disposing
                    conn.ConnectionFailed -= OnConnectionFailed;
                    conn.ConnectionRestored -= OnConnectionRestored;
                    conn.ErrorMessage -= OnErrorMessage;
                });
                
                await Try.IgnoringError(async () => await conn.DisposeAsync());
            }
        }
        
        

        public async Task<IAsyncDisposable> SubscribeToChannel(string channelName, Func<ChannelMessage, Task> onMessage)
        {
            
            channelName = "channel:" + keyPrefix + ":" + channelName;
            // TODO ever call needs to respect the cancellation token
            // var channel = await Connection.GetSubscriber()
            //     .SubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal));
            //
            // channel.OnMessage(onMessage);

            var resilientSubscriber = new ResilientSubscriber(this.Connection,
                channelName,
                onMessage,
                facadeCancellationToken,
                () => ConnectionInErrorHelperProvider(),
                log
            );

            await resilientSubscriber.StartSubscribe();
            
            return resilientSubscriber;
        }
        
        public async Task PublishToChannel(string channelName, string payload)
        {
            channelName = "channel:" + keyPrefix + ":" + channelName;
            var subscriber = Connection.GetSubscriber();
            await subscriber.PublishAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal), payload);
        }
        
        public async Task SetInHash(string key, string field, string payload)
        {
            key = "hash:" + keyPrefix + ":" + key;
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
            key = "hash:" + keyPrefix + ":" + key;
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
            key = "list:" + keyPrefix + ":" + key;
            var database = Connection.GetDatabase();
            // TODO can we set TTL on this?
            await database.ListRightPushAsync(key, payload);
        }

        public async Task<string?> ListLeftPopAsync(string key)
        {
            key = "list:" + keyPrefix + ":" + key;
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
            key = "string:" + keyPrefix + ":" + key;
            var database = Connection.GetDatabase();
            await database.StringSetAsync(key, value);
        }
        
        public async Task<string?> GetString(string key)
        {
            key = "string:" + keyPrefix + ":" + key;
            var database = Connection.GetDatabase();
            return await database.StringGetAsync(key);
        }
    }

}