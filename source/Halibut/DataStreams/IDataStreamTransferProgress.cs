using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.DataStreams
{
    public interface IDataStreamTransferProgress
    {
        /// <summary>
        /// Called as Halibut writes the DataStream contents to the network.
        /// On distributed calls, it is possible that this will never be called OR it may be
        /// called few seconds. It is possible that this is never called when the dataStream
        /// is completly sent.
        /// </summary>
        /// <param name="copiedSoFar"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Progress(long copiedSoFar, CancellationToken cancellationToken);

        
        /// <summary>
        /// Always called once the DataStream will not be uploaded further., this may be because:
        ///  - the DataStream is completely uploaded.
        ///  - the upload failed.
        ///  - the upload never started.
        ///
        // This is particularly relevant for distributed queues where, a Progress call may not be
        // made where copiedSoFar == total length. 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task NoLongerUploading(CancellationToken cancellationToken);
    }
}