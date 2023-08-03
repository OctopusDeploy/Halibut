using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestMessageSerializerObserver : IMessageSerializerObserver
    {
        readonly List<WrittenMessage> messagesWritten = new();
        readonly List<ReadMessage> messagesRead = new();

        public IReadOnlyList<WrittenMessage> MessagesWritten => messagesWritten;
        public IReadOnlyList<ReadMessage> MessagesRead => messagesRead;

        public void MessageWritten(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            var writtenMessage = new WrittenMessage(compressedBytesWritten, compressedBytesWrittenIntoMemory);
            messagesWritten.Add(writtenMessage);
        }

        public void MessageRead(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            var readMessage = new ReadMessage(compressedBytesRead, decompressedBytesRead, decompressedBytesReadIntoMemory);
            messagesRead.Add(readMessage);
        }
    }
}