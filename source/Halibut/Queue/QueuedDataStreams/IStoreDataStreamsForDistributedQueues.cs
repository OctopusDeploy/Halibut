using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public interface IStoreDataStreamsForDistributedQueues
    {
        public Task StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken);
        
        public Task ReHydrateDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken);
    }
}