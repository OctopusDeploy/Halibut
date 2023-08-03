namespace Halibut.Tests.Support
{
    public class ReceivedMessageState
    {
        public long CompressedBytesRead { get; }
        public long DecompressedBytesRead { get; }
        public long DecompressedBytesReadIntoMemory { get; }

        public ReceivedMessageState(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            CompressedBytesRead = compressedBytesRead;
            DecompressedBytesRead = decompressedBytesRead;
            DecompressedBytesReadIntoMemory = decompressedBytesReadIntoMemory;
        }
    }
}