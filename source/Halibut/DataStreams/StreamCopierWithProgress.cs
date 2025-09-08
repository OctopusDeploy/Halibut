using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.DataStreams
{
    public class StreamCopierWithProgress
    {
        const int BufferSize = 84000;
        readonly Stream source;
        readonly IDataStreamTransferProgress dataStreamTransferProgress;

        public StreamCopierWithProgress(Stream source, IDataStreamTransferProgress dataStreamTransferProgress)
        {
            this.source = source;
            this.dataStreamTransferProgress = dataStreamTransferProgress;
        }
        
        public async Task CopyAndReportProgressAsync(Stream destination, CancellationToken cancellationToken)
        {
            try
            {
                var readBuffer = new byte[BufferSize];
                var writeBuffer = new byte[BufferSize];

                var totalLength = source.Length;
                long copiedSoFar = 0;
                source.Seek(0, SeekOrigin.Begin);

                var count = await source.ReadAsync(readBuffer, 0, BufferSize, cancellationToken);
                while (count > 0)
                {
                    Swap(ref readBuffer, ref writeBuffer);
                    var writeTask = destination.WriteAsync(writeBuffer, 0, count, cancellationToken);
                    count = await source.ReadAsync(readBuffer, 0, BufferSize, cancellationToken);
                    await writeTask;

                    copiedSoFar += count;

                    await dataStreamTransferProgress.Progress(copiedSoFar, cancellationToken);
                }

                await destination.FlushAsync(cancellationToken);
            }
            finally
            {
                await dataStreamTransferProgress.NoLongerUploading(cancellationToken);
            }
        }

        static void Swap<T>(ref T x, ref T y)
        {
            T tmp = x;
            x = y;
            y = tmp;
        }
    }
}