using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        [Obsolete]
        void WriteMessage<T>(Stream stream, T message);
        Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken);

        [Obsolete]
        T ReadMessage<T>(RewindableBufferStream stream);
        Task<T> ReadMessageAsync<T>(RewindableBufferStream stream, CancellationToken cancellationToken);
    }
}