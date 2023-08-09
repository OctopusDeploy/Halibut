using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileDataStreamReceiver : IDataStreamReceiver
    {
        readonly Action<Stream> writer;
        readonly Func<Stream, CancellationToken, Task> writerAsync;

        public TemporaryFileDataStreamReceiver(Action<Stream> writer, Func<Stream, CancellationToken, Task> writerAsync)
        {
            this.writer = writer;
            this.writerAsync = writerAsync;
        }

        public void SaveTo(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                writer(file);
            }
        }

        public async Task SaveToAsync(string filePath, CancellationToken cancellationToken)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                await writerAsync(file, cancellationToken);
            }
        }

        public void Read(Action<Stream> reader)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
                {
                    writer(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    reader(fileStream);
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        public async Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
                {
                    await writerAsync(fileStream, cancellationToken);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    await readerAsync(fileStream, cancellationToken);
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