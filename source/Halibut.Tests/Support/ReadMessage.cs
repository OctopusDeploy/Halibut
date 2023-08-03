namespace Halibut.Tests.Support
{
    public class ReadMessage
    {
        public long CompressedBytesRead { get; }
        public long DecompressedBytesRead { get; }
        public long DecompressedBytesReadIntoMemory { get; }

        public ReadMessage(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            CompressedBytesRead = compressedBytesRead;
            DecompressedBytesRead = decompressedBytesRead;
            DecompressedBytesReadIntoMemory = decompressedBytesReadIntoMemory;
        }
    }
}