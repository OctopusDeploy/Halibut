using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestMessageSerializerObserver : IMessageSerializerObserver
    {
        readonly List<MessageWriteObservation> messagesWritten = new();
        readonly List<MessageReadObservation> messagesRead = new();

        public IReadOnlyList<MessageWriteObservation> MessagesWritten => messagesWritten;
        public IReadOnlyList<MessageReadObservation> MessagesRead => messagesRead;

        public void MessageWritten(long compressedBytesWritten, long compressedBytesWrittenIntoMemory)
        {
            var writtenMessage = new MessageWriteObservation(compressedBytesWritten, compressedBytesWrittenIntoMemory);
            messagesWritten.Add(writtenMessage);
        }

        public void MessageRead(long compressedBytesRead, long decompressedBytesRead, long decompressedBytesReadIntoMemory)
        {
            var readMessage = new MessageReadObservation(compressedBytesRead, decompressedBytesRead, decompressedBytesReadIntoMemory);
            messagesRead.Add(readMessage);
        }
    }
}