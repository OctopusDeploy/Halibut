using System;

namespace Halibut.DataStreams
{
    public interface IDataStreamWithFileUploadProgress
    {
        IDataStreamTransferProgress DataStreamTransferProgress { get; }
        
        long Length { get; }
        
        Guid Id { get; }
    }
}