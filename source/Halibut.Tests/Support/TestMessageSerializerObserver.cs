using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestMessageSerializerObserver : IMessageSerializerObserver
    {
        readonly List<WrittenMessageState> messagesWritten = new();
        readonly List<ReceivedMessageState> messagesRead = new();

        public IReadOnlyList<WrittenMessageState> MessagesWritten => messagesWritten;
        public IReadOnlyList<ReceivedMessageState> MessagesRead => messagesRead;

        public void MessageWritten(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            var writtenMessage = new WrittenMessageState(compressedBytesWritten, compressedBytesWrittenIntoMemory);
            messagesWritten.Add(writtenMessage);
        }

        public void MessageRead(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            var readMessage = new ReceivedMessageState(compressedBytesRead, decompressedBytesRead, decompressedBytesReadIntoMemory);
            messagesRead.Add(readMessage);
        }
    }
}