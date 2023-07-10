namespace Halibut.Tests.Support
{
    public class ReceivedMessageState
    {
        public long CompressedBytesRead { get; }
        public long DecompressedBytesRead { get; }

        public ReceivedMessageState(long compressedBytesRead, long decompressedBytesRead)
        {
            CompressedBytesRead = compressedBytesRead;
            DecompressedBytesRead = decompressedBytesRead;
        }
    }
}