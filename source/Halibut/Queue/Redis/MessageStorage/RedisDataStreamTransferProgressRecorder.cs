using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.DataStreams;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class RedisDataStreamTransferProgressRecorder : IDataStreamTransferProgress
    {
        long copiedSoFar;

        /// <summary>
        /// How much data has been transferred so far.
        /// </summary>
        public long CopiedSoFar => Interlocked.Read(ref copiedSoFar);

        public long TotalLength { get; }
        public Guid DataStreamId { get; }
        public RedisDataStreamTransferProgressRecorder(DataStream dataStream)
        {
            TotalLength = dataStream.Length;
            DataStreamId = dataStream.Id;
        }
        
        public async Task Progress(long copiedSoFar, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            Interlocked.Exchange(ref this.copiedSoFar, copiedSoFar);
        }

        public async Task NoLongerUploading(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
}