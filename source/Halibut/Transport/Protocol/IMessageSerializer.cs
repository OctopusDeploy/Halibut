using System.IO;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        void WriteMessage<T>(Stream stream, T message);

        Task<T> ReadMessage<T>(Stream stream);
    }
}