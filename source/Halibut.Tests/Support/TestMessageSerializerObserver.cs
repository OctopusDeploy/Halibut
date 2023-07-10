using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestMessageSerializerObserver : IMessageSerializerObserver
    {
        readonly List<long> messagesWritten = new();
        readonly List<ReadMessage> messagesRead = new();

        public IReadOnlyList<long> MessagesWritten => messagesWritten;
        public IReadOnlyList<ReadMessage> MessagesRead => messagesRead;

        public void MessageWritten(long compressedBytesWritten)
        {
            messagesWritten.Add(compressedBytesWritten);
        }

        public void MessageRead(long compressedBytesRead, long decompressedBytesRead)
        {
            var readMessage = new ReadMessage(compressedBytesRead, decompressedBytesRead);
            messagesRead.Add(readMessage);
        }
    }
}