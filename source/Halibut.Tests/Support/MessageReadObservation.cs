namespace Halibut.Tests.Support
{
    public class MessageReadObservation
    {
        public long CompressedBytesRead { get; }
        public long DecompressedBytesRead { get; }
        public long DecompressedBytesReadIntoMemory { get; }

        public MessageReadObservation(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            CompressedBytesRead = compressedBytesRead;
            DecompressedBytesRead = decompressedBytesRead;
            DecompressedBytesReadIntoMemory = decompressedBytesReadIntoMemory;
        }
    }
}