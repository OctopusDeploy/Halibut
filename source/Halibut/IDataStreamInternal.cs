using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut
{
    internal interface IDataStreamInternal
    {
        void Received(IDataStreamReceiver receiver);
        void Transmit(Stream stream);
        Task TransmitAsync(Stream stream, CancellationToken cancellationToken);
    }
}