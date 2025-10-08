using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.MessageStorage;

namespace Halibut.Queue.QueuedDataStreams
{
    /// <summary>
    /// The Redis Queue requires that something else can store data streams. The
    /// Redis Queue will call this interface for storage and retrieval of data streams.
    ///
    /// The RehydrateDataStreams method will be called at most once, and each data stream passed to
    /// RehydrateDataStreams will be read at most once. Thus, it is safe to delete the DataStream from
    /// storage once the DataStream `writerAsync` Func is called and will no longer return any more
    /// data. This includes in the case the writerAsync method throws.
    /// </summary>
    public interface IStoreDataStreamsForDistributedQueues
    {
        /// <summary>
        /// Must store the data for the given dataStreams.
        /// </summary>
        /// <param name="dataStreams"></param>
        /// <param name="useReciever">When set 'true' the data must be read from the Receiver of the DataStream. This will be true for responses.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A string, DataStreamMetadata, containing a small amount of data that will be stored in redis, this will be
        /// given to RehydrateDataStreams</returns>
        public Task<byte[]> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, bool useReciever, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the dataStreams `writerAsync` to write the previously stored data. Using
        /// the SetWriterAsync method. 
        /// </summary>
        /// <param name="dataStreamMetadata"></param>
        /// <param name="dataStreams"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task RehydrateDataStreams(byte[] dataStreamMetadata, List<IRehydrateDataStream> dataStreams, CancellationToken cancellationToken);
    }
}