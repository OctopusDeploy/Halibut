using System;

namespace Halibut.TestUtils.Contracts
{
    public class ReadDataStreamService : IReadDataStreamService
    {
        long SendData(DataStream dataStream)
        {
            long total = 0;
            dataStream.Receiver().Read(reader =>
            {
                var buf = new byte[1024];
                while (true)
                {
                    int read = reader.Read(buf, 0, buf.Length);
                    if(read == 0) break;
                    total += read;
                }
            });

            return total;
        }

        public long SendData(params DataStream[] dataStreams)
        {
            long count = 0;
            foreach (var dataStream in dataStreams)
            {
                count += SendData(dataStream);
            }

            return count;
        }
    }
}
