using System.IO;

namespace Halibut.Tests.Util.TcpUtils
{
    public interface IDataTransferObserver
    {
        public void WritingData(TcpPump tcpPump, MemoryStream buffer);
    }
}