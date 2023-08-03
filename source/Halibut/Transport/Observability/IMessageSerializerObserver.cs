namespace Halibut.Transport.Observability
{
    public interface IMessageSerializerObserver
    {
        void MessageWritten(long compressedBytesWritten, long compressedBytesWrittenIntoMemory);
        void MessageRead(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory);
    }
}