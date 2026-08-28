using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class InMemoryDataStreamReceiver : IDataStreamReceiver
    {
        readonly Func<Stream, CancellationToken, Task> writerAsync;

        public InMemoryDataStreamReceiver(Func<Stream, CancellationToken, Task> writerAsync)
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
                await SaveToStreamAsync(file, cancellationToken);
            }
        }
        
        public async Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken)
        {
            await writerAsync(destinationStream, cancellationToken);
            
        }
        
        public async Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken)
        {
            using (var stream = new MemoryStream())
            {
                await writerAsync(stream, cancellationToken);
                stream.Seek(0, SeekOrigin.Begin);
                await readerAsync(stream, cancellationToken);
            }
        }
    }
}