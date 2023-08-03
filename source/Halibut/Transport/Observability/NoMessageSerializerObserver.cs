using System;

namespace Halibut.Transport.Observability
{
    public class NoMessageSerializerObserver : IMessageSerializerObserver
    {
        public void MessageWritten(long compressedBytesWritten, long bytesWrittenIntoMemory)
        {
        }

        public void MessageRead(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
        }
    }
}