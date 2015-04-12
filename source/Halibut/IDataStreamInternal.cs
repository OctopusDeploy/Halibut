using System;
using System.IO;

namespace Halibut
{
    internal interface IDataStreamInternal
    {
        void Received(IDataStreamReceiver receiver);
        void Transmit(Stream stream);
    }
}