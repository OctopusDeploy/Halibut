using System;
using System.IO;

namespace Halibut.Transport.Protocol
{
    public interface IMessageSerializer
    {
        void WriteMessage<T>(Stream stream, T message);

        T ReadMessage<T>(Stream stream);

        void AddToMessageContract(params Type[] types);
    }
}