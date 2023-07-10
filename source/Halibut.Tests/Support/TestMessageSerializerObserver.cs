using System.Collections.Generic;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class TestMessageSerializerObserver : IMessageSerializerObserver
    {
        readonly List<long> messagesWritten = new();
        readonly List<long> messagesRead = new();

        public IReadOnlyList<long> MessagesWritten => messagesWritten;
        public IReadOnlyList<long> MessagesRead => messagesRead;

        public void MessageWritten(long compressedBytesWritten)
        {
            messagesWritten.Add(compressedBytesWritten);
        }

        public void MessageRead(long decompressedBytesRead)
        {
            messagesRead.Add(decompressedBytesRead);
        }
    }
}