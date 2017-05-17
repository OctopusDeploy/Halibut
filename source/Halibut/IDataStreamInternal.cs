using System;
using System.IO;
using System.Threading.Tasks;

namespace Halibut
{
    interface IDataStreamInternal
    {
        void SetReceived(IDataStreamReceiver receiver);
        Task Transmit(Stream stream);
    }
}