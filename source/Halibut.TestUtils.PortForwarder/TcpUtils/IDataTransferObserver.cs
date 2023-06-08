using System;
using System.IO;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.TestUtils.PortForwarder.TcpUtils
{
    public interface IDataTransferObserver
    {
        public void WritingData(TcpPump tcpPump, MemoryStream buffer);
    }
}