using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileDataStreamReceiver : IDataStreamReceiver
    {
        readonly Func<Stream, CancellationToken, Task> writerAsync;

        public TemporaryFileDataStreamReceiver(Func<Stream, CancellationToken, Task> writerAsync)
        {
            this.writerAsync = writerAsync;
        }

        public async Task SaveToAsync(string filePath, CancellationToken cancellationToken)
        {
#if !NETFRAMEWORK
            await
#endif
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                await writerAsync(file, cancellationToken);
            }
        }
        
        public async Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
#if !NETFRAMEWORK
                await
#endif
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