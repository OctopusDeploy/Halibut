using System.IO;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        void WriteMessage<T>(Stream stream, T message);

        T ReadMessage<T>(RewindableBufferStream stream);
    }
}