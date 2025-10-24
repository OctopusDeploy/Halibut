using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.DataStreams
{
    public class PercentageCompleteDataStreamTransferProgress : IDataStreamTransferProgress
    {
        readonly Func<int, CancellationToken, Task> updateProgressAsync;
        readonly long totalLength;
        
        const int UploadNotStartedValue = -1;
        int progress = UploadNotStartedValue;

        public PercentageCompleteDataStreamTransferProgress(Func<int, CancellationToken, Task> updateProgressAsync, long totalLength)
        {
            this.updateProgressAsync = updateProgressAsync;
            this.totalLength = totalLength;
        }

        public async Task Progress(long copiedSoFar, CancellationToken cancellationToken)
        {
            var progressNow = (int)((double)copiedSoFar / totalLength * 100.00);
            if (progressNow != progress)
            {
                await updateProgressAsync(progressNow, cancellationToken);
                progress = progressNow;
            }
        }

        public async Task NoLongerUploading(CancellationToken cancellationToken)
        {
            if (progress != 100 && progress != UploadNotStartedValue)
            {
                // Just set it to 100% complete.
                // It is 100% as uploaded as it will ever be :D
                await updateProgressAsync(100, cancellationToken);
            }
        }
    }
}