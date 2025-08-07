using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis;
using Halibut.ServiceModel;
using Halibut.Tests.Queue;
using Halibut.Tests.Queue.Redis.Utils;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using DisposableCollection = Halibut.Util.DisposableCollection;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Builders
{

    public interface IPendingRequestQueueBuilder
    {
        public IPendingRequestQueueBuilder WithEndpoint(string endpoint);
        public IPendingRequestQueueBuilder WithLog(ILog log);
        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout);
        public QueueHolder Build();
    }
    
    public class RedisPendingRequestQueueBuilder : IPendingRequestQueueBuilder
    {
        
        const int redisPort = 6379;
        
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

        public IPendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public IPendingRequestQueueBuilder WithLog(ILog log)
        {
            this.log = log;
            return this;
        }

        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public QueueHolder Build()
        {
            var endpoint = new Uri(this.endpoint ?? "poll://endpoint001");
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var log = this.log ?? new TestContextLogCreator("Queue", LogLevel.Trace).CreateNewForPrefix("");

            if (this.pollingQueueWaitTimeout != null)
            {
                halibutTimeoutsAndLimits.PollingQueueWaitTimeout = pollingQueueWaitTimeout.Value;
            }

            var disposableCollection = new DisposableCollection();

            var redisFacade = new RedisFacade("localhost:" + redisPort, (Guid.NewGuid()).ToString(), log);
            disposableCollection.AddAsyncDisposable(redisFacade);
            
            var redisTransport = new HalibutRedisTransport(redisFacade);
            var dataStreamStore = new InMemoryStoreDataStreamsForDistributedQueues();
            var messageSerializer = new QueueMessageSerializerBuilder().Build();
            var messageReaderWriter = new MessageReaderWriter(messageSerializer, dataStreamStore);

            var request = new RequestMessageBuilder("poll://test-endpoint").Build();

            var queue = new RedisPendingRequestQueue(endpoint, new NeverLosingDataWatchForRedisLosingAllItsData(), log, redisTransport, messageReaderWriter, halibutTimeoutsAndLimits);
            
#pragma warning disable VSTHRD002
            queue.WaitUntilQueueIsSubscribedToReceiveMessages().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            
            return new QueueHolder(queue, disposableCollection);
        }
    }
    
    public class PendingRequestQueueBuilder : IPendingRequestQueueBuilder
    {
        ILog? log;
        string? endpoint;
        TimeSpan? pollingQueueWaitTimeout;

        public IPendingRequestQueueBuilder WithEndpoint(string endpoint)
        {
            this.endpoint = endpoint;
            return this;
        }

        public IPendingRequestQueueBuilder WithLog(ILog log)
        {
            this.log = log;
            return this;
        }

        public IPendingRequestQueueBuilder WithPollingQueueWaitTimeout(TimeSpan? pollingQueueWaitTimeout)
        {
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            return this;
        }

        public QueueHolder Build()
        {
            var endpoint = this.endpoint ?? "poll://endpoint001";
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var log = this.log ?? new InMemoryConnectionLog(endpoint);

            var pollingQueueWaitTimeout = this.pollingQueueWaitTimeout ?? halibutTimeoutsAndLimits.PollingQueueWaitTimeout;

            return new QueueHolder(new PendingRequestQueueAsync(log, pollingQueueWaitTimeout), new DisposableCollection());
        }
    }

    public class QueueHolder : IAsyncDisposable
    {
        public IPendingRequestQueue PendingRequestQueue { get; }
        public DisposableCollection DisposableCollection { get; }

        public QueueHolder(IPendingRequestQueue pendingRequestQueue, Halibut.Util.DisposableCollection disposableCollection)
        {
            this.PendingRequestQueue = pendingRequestQueue;
            this.DisposableCollection = disposableCollection;
        }

        public async ValueTask DisposeAsync()
        {
            this.DisposableCollection.AddAsyncDisposable(PendingRequestQueue);
            await DisposableCollection.DisposeAsync();
        }
    }
}