using System;

namespace Halibut.Transport.Observability
{
    public class NoMessageSerializerObserver : IMessageSerializerObserver
    {
        public void MessageWritten(long compressedBytesWritten)
        {
        }

        public void MessageRead(long decompressedBytesRead)
        {
        }
    }
}