using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    /// <summary>
    /// The Redis Queue requires that something else can store data streams. The
    /// Redis Queue will call this interface for storage and retrieval of data streams.
    ///
    /// The ReHydrateDataStreams method will be called at most once, and each data stream passed to
    /// ReHydrateDataStreams will be read at most once. Thus, it is safe to delete the DataStream from
    /// storage once the DataStream `writerAsync` Func is called and will no longer return any more
    /// data. This includes in the case the writerAsync method throws.
    /// </summary>
    public interface IStoreDataStreamsForDistributedQueues
    {
        /// <summary>
        /// Must store the data for the given dataStreams.
        /// </summary>
        /// <param name="dataStreams"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A string, DataStreamMetadata, containing a small amount of data that will be stored in redis, this will be
        /// given to ReHydrateDataStreams</returns>
        public Task<string> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the dataStreams `writerAsync` to write the previously stored data. Using
        /// the SetWriterAsync method. 
        /// </summary>
        /// <param name="dataStreamMetadata"></param>
        /// <param name="dataStreams"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ReHydrateDataStreams(string dataStreamMetadata, IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken);
    }
}