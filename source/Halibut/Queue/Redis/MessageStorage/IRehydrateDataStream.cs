using System;
using System.IO;

namespace Halibut.Queue.Redis.MessageStorage
{
    public interface IRehydrateDataStream
    {
        public Guid Id { get; }
        public long Length { get; }
        
        // Rehydrates the DataStream with the stream returned by data.
        // Once that stream is returned that stream will be disposed as well
        // as the disposable if not null.
        public void Rehydrate(Func<(Stream, IAsyncDisposable?)> data);
    }
}