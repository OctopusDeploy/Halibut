namespace Halibut.Tests.Support
{
    public class WrittenMessage
    {
        public long CompressedBytesWritten { get; }
        public long CompressedBytesWrittenIntoMemory { get; }

        public WrittenMessage(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            CompressedBytesWritten = compressedBytesWritten;
            CompressedBytesWrittenIntoMemory = compressedBytesWrittenIntoMemory;
        }
    }
}