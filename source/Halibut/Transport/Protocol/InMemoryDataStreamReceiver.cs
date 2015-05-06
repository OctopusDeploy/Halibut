using System;
using System.IO;

namespace Halibut.Transport.Protocol
{
    public class InMemoryDataStreamReceiver : IDataStreamReceiver
    {
        readonly Action<Stream> writer;

        public InMemoryDataStreamReceiver(Action<Stream> writer)
        {
            this.writer = writer;
        }

        public void SaveTo(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                writer(file);
            }
        }

        public void Read(Action<Stream> reader)
        {
            using (var stream = new MemoryStream())
            {
                writer(stream);
                stream.Seek(0, SeekOrigin.Begin);
                reader(stream);
            }
        }
    }
}
