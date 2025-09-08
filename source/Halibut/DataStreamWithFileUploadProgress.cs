using System;
using System.IO;
using Halibut.DataStreams;

namespace Halibut
{
    public class DataStreamWithFileUploadProgress : DataStream, IDataStreamWithFileUploadProgress, ICanBeSwitchedToNotReportProgress
    {
        public IDataStreamTransferProgress DataStreamTransferProgress { get; }
        internal Stream OriginalStream { get; }

        public DataStreamWithFileUploadProgress(Stream stream, IDataStreamTransferProgress dataStreamTransferProgress)
        {
            OriginalStream = stream;
            DataStreamTransferProgress = dataStreamTransferProgress;
            Length = stream.Length;
            Id = Guid.NewGuid();
            writerAsync = new StreamCopierWithProgress(stream, this.DataStreamTransferProgress).CopyAndReportProgressAsync;
        }

        public void SwitchWriterToNotReportProgress()
        {
            writerAsync = new StreamCopierWithProgress(OriginalStream, new NoOpDataStreamTransferProgress()).CopyAndReportProgressAsync;
        }
    }
}