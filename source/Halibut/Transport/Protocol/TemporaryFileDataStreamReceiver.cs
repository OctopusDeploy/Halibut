using System;
using System.IO;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileDataStreamReceiver : IDataStreamReceiver
    {
        readonly Action<Stream> writer;

        public TemporaryFileDataStreamReceiver(Action<Stream> writer)
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
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
            {
                writer(fileStream);
                fileStream.Seek(0, SeekOrigin.Begin);
                reader(fileStream);
            }
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}