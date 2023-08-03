namespace Halibut.Tests.Support
{
    public class MessageWriteObservation
    {
        public long CompressedBytesWritten { get; }
        public long CompressedBytesWrittenIntoMemory { get; }

        public MessageWriteObservation(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            CompressedBytesWritten = compressedBytesWritten;
            CompressedBytesWrittenIntoMemory = compressedBytesWrittenIntoMemory;
        }
    }
}