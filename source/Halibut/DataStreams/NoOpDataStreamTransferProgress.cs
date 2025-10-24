using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.DataStreams
{
    public class NoOpDataStreamTransferProgress : IDataStreamTransferProgress
    {
        public Task Progress(long copiedSoFar, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NoLongerUploading(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}