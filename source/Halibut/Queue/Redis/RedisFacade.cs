using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Halibut.Diagnostics; // Add logging support
using StackExchange.Redis;

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
        // We can survive redis being unavailable for this amount of time.
        internal TimeSpan MaxDurationToRetryFor = TimeSpan.FromSeconds(30);
        
        ConnectionMultiplexer Connection => connection.Value;
        
        string keyPrefix;


        CancelOnDisposeCancellationToken cts;
        CancellationToken facadeCancellationToken;

        public RedisFacade(string configuration, string keyPrefix, ILog log) : this(ConfigurationOptions.Parse(configuration), keyPrefix, log)
        {
            
        }
        public RedisFacade(ConfigurationOptions redisOptions, string keyPrefix, ILog log)
        {
            this.keyPrefix = keyPrefix ?? "halibut";
            this.log = log;
            this.cts = new CancelOnDisposeCancellationToken();
            this.facadeCancellationToken = cts.Token;

            // aka have more goes at connecting.
            redisOptions.AbortOnConnectFail = false;
            
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
        
        private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
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

        /// <summary>
        /// Executes an operation with retry logic. Retries for up to 12 seconds with 1-second intervals.
        /// </summary>
        private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
        {
            await using var linkedTokenSource = new CancelOnDisposeCancellationToken(cancellationToken, facadeCancellationToken);
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

        /// <summary>
        /// Executes an operation with retry logic. Retries for up to 12 seconds with 1-second intervals.
        /// </summary>
        private async Task ExecuteWithRetry(Func<Task> operation, CancellationToken cancellationToken)
        {
            await using var linkedTokenSource = new CancelOnDisposeCancellationToken(cancellationToken, facadeCancellationToken);
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
            await Try.IgnoringError(async () => await cts.DisposeAsync());
            
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
            channelName = "channel:" + keyPrefix + ":" + channelName;
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
        
        public async Task PublishToChannel(string channelName, string payload, CancellationToken cancellationToken)
        {
            channelName = "channel:" + keyPrefix + ":" + channelName;
            await ExecuteWithRetry(async () =>
            {
                var subscriber = Connection.GetSubscriber();
                await subscriber.PublishAsync(new RedisChannel(channelName, RedisChannel.PatternMode.Literal), payload);
            }, cancellationToken);
        }
        
        public async Task SetInHash(string key, string field, string payload, TimeSpan ttl, CancellationToken cancellationToken)
        {
            key = "hash:" + keyPrefix + ":" + key;
            
            // Retry each operation independently
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.HashSetAsync(key, new RedisValue(field), new RedisValue(payload));
            }, cancellationToken);

            await SetTtlForKeyRaw(key, ttl, cancellationToken);
        }

        public async Task<bool> HashContainsKey(string key, string field, CancellationToken cancellationToken)
        {
            key = "hash:" + keyPrefix + ":" + key;
            return await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.HashExistsAsync(key, new RedisValue(field));
            }, cancellationToken);
        }

        public async Task<string?> TryGetAndDeleteFromHash(string key, string field, CancellationToken cancellationToken)
        {
            key = "hash:" + keyPrefix + ":" + key;
            
            // Retry each operation independently
            var value = await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.HashGetAsync(key, new RedisValue(field));
            }, cancellationToken);
            
            // TODO: If we retry this is not idempotent.
            // TODO: This needs to be tested in RedisPendingRequestsQueueFixture
            var res = await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.KeyDeleteAsync(key);
            }, cancellationToken);
            
            if (!res)
            {
                // Someone else deleted this, so return nothing to make the get and delete appear to be atomic. 
                return null;
            } 
            return (string?)value;
        }

        public async Task ListRightPushAsync(string key, string payload, TimeSpan ttlForAllInList, CancellationToken cancellationToken)
        {
            key = "list:" + keyPrefix + ":" + key;
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.ListRightPushAsync(key, payload);
            }, cancellationToken);

            await SetTtlForKeyRaw(key, ttlForAllInList, cancellationToken);        }

        public async Task<string?> ListLeftPopAsync(string key, CancellationToken cancellationToken)
        {
            key = "list:" + keyPrefix + ":" + key;
            return await ExecuteWithRetry<string?>(async () =>
            {
                var database = Connection.GetDatabase();
                var value = await database.ListLeftPopAsync(key);
                if (value.IsNull)
                {
                    return null;
                }

                return (string?)value;
            }, cancellationToken);
        }

        public async Task SetString(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
        {
            key = ToStringKey(key);
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.StringSetAsync(key, value);
            }, cancellationToken);

            await SetTtlForKeyRaw(key, ttl, cancellationToken);
        }

        string ToStringKey(string key)
        {
            return "string:" + keyPrefix + ":" + key;
        }

        public async Task SetTtlForString(string key, TimeSpan ttl, CancellationToken cancellationToken)
        {
            await SetTtlForKeyRaw(ToStringKey(key), ttl, cancellationToken);

        }

        async Task SetTtlForKeyRaw(string key, TimeSpan ttl, CancellationToken cancellationToken)
        {
            await ExecuteWithRetry(async () =>
            {
                var database = Connection.GetDatabase();
                await database.KeyExpireAsync(key, ttl);
            }, cancellationToken);
        }

        public async Task<string?> GetString(string key, CancellationToken cancellationToken)
        {
            key = ToStringKey(key);
            return await ExecuteWithRetry<string?>(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.StringGetAsync(key);
            }, cancellationToken);
        }
        
        public async Task<bool> DeleteString(string key, CancellationToken cancellationToken)
        {
            key = ToStringKey(key);
            return await ExecuteWithRetry<bool>(async () =>
            {
                var database = Connection.GetDatabase();
                return await database.KeyDeleteAsync(key);
            }, cancellationToken);
        }
    }
}