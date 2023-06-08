using System;
using System.IO;

namespace Octopus.TestPortForwarder
{
    public interface IDataTransferObserver
    {
        public void WritingData(TcpPump tcpPump, MemoryStream buffer);
    }
}