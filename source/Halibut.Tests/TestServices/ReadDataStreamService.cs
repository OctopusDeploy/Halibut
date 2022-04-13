using System.Xml.Schema;

namespace Halibut.Tests.TestServices
{
    public class ReadDataStreamService : IReadDataSteamService
    {
        public long SendData(DataStream dataStream)
        {
            long total = 0;
            dataStream.Receiver().Read(reader =>
            {
                var buf = new byte[1024];
                while (true)
                {
                    int read = reader.Read(buf);
                    if(read == 0) break;
                    total += read;
                }
            });
            
            return total;
        }
    }
}