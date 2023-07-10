namespace Halibut.Transport.Observability
{
    public interface IMessageSerializerObserver
    {
        void MessageWritten(long compressedBytesWritten);
        void MessageRead(long decompressedBytesRead);
    }
}