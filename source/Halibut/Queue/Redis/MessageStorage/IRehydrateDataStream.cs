using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Queue.Redis.MessageStorage
{
    public interface IRehydrateDataStream
    {
        public Guid Id { get; }
        public long Length { get; }
        
        // Rehydrates the DataStream with the stream returned by data.
        // Once that stream is returned that stream will be disposed as well
        // as the disposable if not null.
        public void Rehydrate(Func<DataStreamRehydrationData> dataStreamRehydratorFactory);
    }

    public class DataStreamRehydrationData : IAsyncDisposable
    {
        public Stream Data { get; }
        readonly IAsyncDisposable? asyncDisposable;

        public DataStreamRehydrationData(Stream data)
        {
            Data = data;
        }

        public DataStreamRehydrationData(Stream data, IAsyncDisposable? asyncDisposable)
        {
            Data = data;
            this.asyncDisposable = asyncDisposable;
        }

        public async ValueTask DisposeAsync()
        {
            // Failure in dispose would mean we fail requests that don't need to fail.
            await Try.IgnoringError(async () =>
            {
                try
                {
#if NET8_0_OR_GREATER
                    await Data.DisposeAsync();
#else
                Data.Dispose();
#endif
                }
                finally
                {
                    if (asyncDisposable != null) await asyncDisposable.DisposeAsync();
                }
            });
        }
    }
}