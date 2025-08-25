using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;
using StackExchange.Redis;

namespace Halibut.Queue.Redis.RedisHelpers
{
    public class RedisFacade : IAsyncDisposable
    {
        readonly Lazy<ConnectionMultiplexer> connection;
        readonly ILog log;
        // We can survive redis being unavailable for this amount of time.
        // Generally redis will try for 5s, we add our own retries to try for longer.
        internal TimeSpan MaxDurationToRetryFor = TimeSpan.FromSeconds(30);
        
        ConnectionMultiplexer Connection => connection.Value;

        /// <summary>
        /// All Keys will be prefixed with this, this allows for multiple halibuts to use
        /// the same redis without interfering with each other.
        /// </summary>
        readonly string keyPrefix;

        readonly CancelOnDisposeCancellationToken objectLifetimeCts;
        readonly CancellationToken objectLifeTimeCancellationToken;

        public RedisFacade(string configuration, string keyPrefix, ILog log) : this(ConfigurationOptions.Parse(configuration), keyPrefix, log)
        {
            
        }
        public RedisFacade(ConfigurationOptions redisOptions, string keyPrefix, ILog log)
        {
            this.keyPrefix = keyPrefix;
            this.log = log.ForContext<RedisFacade>();
            objectLifetimeCts = new CancelOnDisposeCancellationToken();
            objectLifeTimeCancellationToken = objectLifetimeCts.Token;

            // Tells the client to make multiple attempts to create the TCP connection to redis.
            redisOptions.AbortOnConnectFail = false;
            
            connection = new Lazy<ConnectionMultiplexer>(() =>
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                
                // Subscribe to connection events
                multiplexer.ConnectionFailed += OnConnectionFailed;
                multiplexer.ConnectionRestored += OnConnectionRestored;
                multiplexer.ErrorMessage += OnErrorMessage;
                
                return multiplexer;
            });
        }

        void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            log.Write(EventType.Error, "Redis connection failed - EndPoint: {0}, Failure: {1}, Exception: {2}", e.EndPoint, e.FailureType, e.Exception?.Message);
        }

        void OnErrorMessage(object? sender, RedisErrorEventArgs e)
        {
            log.Write(EventType.Error, "Redis error - EndPoint: {0}, Message: {1}", e.EndPoint, e.Message);
        }

        void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            log.Write(EventType.Diagnostic, "Redis connection restored - EndPoint: {0}", e.EndPoint);
        }

        async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
        {
            await using var linkedTokenSource = new CancelOnDisposeCancellationToken(cancellationToken, objectLifeTimeCancellationToken);
            var combinedToken = linkedTokenSource.Token;
            
            var retryDelay = TimeSpan.FromSeconds(1);
            var stopwatch = Stopwatch.StartNew();
            
            while (true)
            {
                combinedToken.ThrowIfCancellationRequested();
                
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (stopwatch.Elapsed < MaxDurationToRetryFor && !combinedToken.IsCancellationRequested)
                {
                    log?.Write(EventType.Diagnostic, $"Redis operation failed, retrying in {retryDelay.TotalSeconds}s: {ex.Message}");
                    await Task.Delay(retryDelay, combinedToken);
                }
            }
        }

        async Task ExecuteWithRetry(Func<Task> operation, CancellationToken cancellationToken)
        {
            await using var linkedTokenSource = new CancelOnDisposeCancellationToken(cancellationToken, objectLifeTimeCancellationToken);
            var combinedToken = linkedTokenSource.Token;
            
            var retryDelay = TimeSpan.FromSeconds(1);
            var stopwatch = Stopwatch.StartNew();
            
            while (true)
            {
                combinedToken.ThrowIfCancellationRequested();
                
                try
                {
                    await operation();
                    return;
                }
                catch (Exception ex) when (stopwatch.Elapsed < MaxDurationToRetryFor && !combinedToken.IsCancellationRequested)
                {
                    log?.Write(EventType.Diagnostic, $"Redis operation failed, retrying in {retryDelay.TotalSeconds}s: {ex.Message}");
                    await Task.Delay(retryDelay, combinedToken);
                }
            }
        }

        public bool IsConnected => connection.IsValueCreated && Connection.IsConnected;

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await objectLifetimeCts.DisposeAsync());
            
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


        internal int TotalSubscribers = 0;
        
        public async Task<IAsyncDisposable> SubscribeToChannel(string channelName, Func<ChannelMessage, Task> onMessage, CancellationToken cancellationToken)
        {
            channelName = ToPrefixedChannelName(channelName);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // This can throw if we are unable to connect to redis.
                    var channel = await Connection.GetSubscriber()
                        .SubscribeAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal));

                    var disposable = new FuncAsyncDisposable(async () =>
                    {
                        Interlocked.Decrement(ref TotalSubscribers);
                        await Try.IgnoringError(async () => await channel.UnsubscribeAsync());
                    });
                    
                    Interlocked.Increment(ref TotalSubscribers);
                    try
                    {
                        // Once we are connected to redis, it seems even if the connection to redis dies.
                        // The client will take care of re-connecting to redis.
                        channel.OnMessage(onMessage);
                    }
                    catch (Exception)
                    {
                        await disposable.DisposeAsync();
                        throw;
                    }

                    return disposable;
                }
                catch (Exception ex)
                {
                    log?.WriteException(EventType.Diagnostic, "Failed to subscribe to Redis channel {0}, retrying in 2 seconds", ex, channelName);
                    await Try.IgnoringError(async () => await Task.Delay(2000, cancellationToken));
                }
            }
        }

        string ToPrefixedChannelName(string channelName)
        {
            return "channel:" + keyPrefix + ":" + channelName;
        }
        
        public async Task PublishToChannel(string channelName, string payload, CancellationToken cancellationToken)
        {
            channelName = ToPrefixedChannelName(channelName);
            await ExecuteWithRetry(async () =>
            {
                var subscriber = Connection.GetSubscriber();
                await subscriber.PublishAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal), payload);
            }, cancellationToken);
        }
        
        public async Task SetInHash(string key, Dictionary<string, byte[]> values, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var hashKey = ToHashKey(key);

            var hashEntries = values.Select(v => new HashEntry(v.Key, v.Value)).ToArray();
            
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.HashSetAsync(hashKey, hashEntries);
            }, cancellationToken);

            await SetTtlForKeyRaw(hashKey, ttl, cancellationToken);
        }

        RedisKey ToHashKey(string key)
        {
            return "hash:" + keyPrefix + ":" + key;
        }
        
        public async Task<bool> HashContainsKey(string key, string field, CancellationToken cancellationToken)
        {
            var hashKey = ToHashKey(key);
            return await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.HashExistsAsync(hashKey, new RedisValue(field));
            }, cancellationToken);
        }

        public async Task<Dictionary<string, byte[]?>?> TryGetAndDeleteFromHash(string key, string[] fields, CancellationToken cancellationToken)
        {
            var hashKey = ToHashKey(key);

            Dictionary<string, byte[]?>? dict = await RawKeyReadHashFieldsToDictionary(hashKey, fields, cancellationToken);
            
            // Retry does make this non-idempotent, what can happen is the key is deleted on redis.
            // But we do not get a response saying it is deleted. We try again and get told
            // it is already deleted.
            // In the Redis Queue this can result in no-body picking up the Request, and the
            // request eventually timing out.
            var res = await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.KeyDeleteAsync(hashKey);
            }, cancellationToken);
            
            if (!res)
            {
                // Someone else deleted this, so return nothing to make the get and delete appear to be atomic. 
                return null;
            } 
            return dict;
        }
        
        public async Task<Dictionary<string, byte[]?>?> TryGetFromHash(string key, string[] fields, CancellationToken cancellationToken)
        {
            var hashKey = ToHashKey(key);

            return await RawKeyReadHashFieldsToDictionary(hashKey, fields, cancellationToken);
        }
        
        
        public async Task DeleteHash(string key, CancellationToken cancellationToken)
        {
            var hashKey = ToHashKey(key);
            
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.KeyDeleteAsync(hashKey);
            }, cancellationToken);
        }
        
        async Task<Dictionary<string, byte[]?>?> RawKeyReadHashFieldsToDictionary(RedisKey hashKey, string[] fields, CancellationToken cancellationToken)
        {
            var dict = new Dictionary<string, byte[]?>();
            foreach (var field in fields)
            {
                // Retry each operation independently
                var value = await ExecuteWithRetry(async () =>
                {
                    var database = Connection.GetDatabase();
                    return await database.HashGetAsync(hashKey, new RedisValue(field));
                }, cancellationToken);
                if(value.HasValue)  dict[field] = value;
            }

            if (dict.Count == 0) return null;
            
            return dict;
        }

        RedisKey ToListKey(string key)
        {
            return "list:" + keyPrefix + ":" + key;
        }

        public async Task ListRightPushAsync(string key, string payload, TimeSpan ttlForAllInList, CancellationToken cancellationToken)
        {
            var listKey = ToListKey(key);
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.ListRightPushAsync(listKey, payload);
            }, cancellationToken);

            await SetTtlForKeyRaw(listKey, ttlForAllInList, cancellationToken);
        }

        public async Task<string?> ListLeftPopAsync(string key, CancellationToken cancellationToken)
        {
            var listKey = ToListKey(key);
            return await ExecuteWithRetry<string?>(async () =>
            {
                var database = Connection.GetDatabase();
                var value = await database.ListLeftPopAsync(listKey);
                if (value.IsNull)
                {
                    return null;
                }

                return value;
            }, cancellationToken);
        }

        RedisKey ToStringKey(string key)
        {
            return "string:" + keyPrefix + ":" + key;
        }

        public async Task SetString(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var stringKey = ToStringKey(key);
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.StringSetAsync(stringKey, value);
            }, cancellationToken);

            await SetTtlForKeyRaw(stringKey, ttl, cancellationToken);
        }

        public async Task SetTtlForString(string key, TimeSpan ttl, CancellationToken cancellationToken)
        {
            await SetTtlForKeyRaw(ToStringKey(key), ttl, cancellationToken);
        }

        public async Task<string?> GetString(string key, CancellationToken cancellationToken)
        {
            var stringKey = ToStringKey(key);
            return await ExecuteWithRetry<string?>(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.StringGetAsync(stringKey);
            }, cancellationToken);
        }
        
        public async Task<bool> DeleteString(string key, CancellationToken cancellationToken)
        {
            var stringKey = ToStringKey(key);
            return await ExecuteWithRetry<bool>(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.KeyDeleteAsync(stringKey);
            }, cancellationToken);
        }
        
        async Task SetTtlForKeyRaw(RedisKey key, TimeSpan ttl, CancellationToken cancellationToken)
        {
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.KeyExpireAsync(key, ttl);
            }, cancellationToken);
        }
    }
}