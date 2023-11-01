using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut
{
    interface IDataStreamInternal
    {
        void Received(IDataStreamReceiver receiver);
        Task TransmitAsync(Stream stream, CancellationToken cancellationToken);
    }
}