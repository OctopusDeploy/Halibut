using System;
using System.IO;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileDataStreamReceiver : IDataStreamReceiver
    {
        readonly Func<Stream, Task> writer;

        public TemporaryFileDataStreamReceiver(Func<Stream, Task> writer)
        {
            this.writer = writer;
        }

        public Task SaveTo(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                return writer(file);
            }
        }

        public async Task Read(Func<Stream, Task> reader)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
                {
                    await writer(fileStream).ConfigureAwait(false);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    await reader(fileStream).ConfigureAwait(false);
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}