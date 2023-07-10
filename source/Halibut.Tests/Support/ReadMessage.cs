namespace Halibut.Tests.Support
{
    public class ReadMessage
    {
        public long CompressedBytesRead { get; }
        public long DecompressedBytesRead { get; }

        public ReadMessage(long compressedBytesRead, long decompressedBytesRead)
        {
            CompressedBytesRead = compressedBytesRead;
            DecompressedBytesRead = decompressedBytesRead;
        }
    }
}