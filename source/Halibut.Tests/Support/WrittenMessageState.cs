namespace Halibut.Tests.Support
{
    public class WrittenMessageState
    {
        public long CompressedBytesWritten { get; }
        public long CompressedBytesWrittenIntoMemory { get; }

        public WrittenMessageState(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            CompressedBytesWritten = compressedBytesWritten;
            CompressedBytesWrittenIntoMemory = compressedBytesWrittenIntoMemory;
        }
    }
}