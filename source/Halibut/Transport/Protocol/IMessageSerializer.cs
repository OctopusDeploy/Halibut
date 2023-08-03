using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        void WriteMessage<T>(Stream stream, T message);
        Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken cancellationToken);

        T ReadMessage<T>(RewindableBufferStream stream);
        Task<T> ReadMessageAsync<T>(RewindableBufferStream stream, CancellationToken cancellationToken);
    }
}