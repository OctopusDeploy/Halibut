using Halibut.Transport.Streams;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class InMemoryDataStreamReceiver : IDataStreamReceiver
    {
        readonly Action<Stream> writer;
        readonly Func<Stream, CancellationToken, Task> writerAsync;

        public InMemoryDataStreamReceiver(Action<Stream> writer, Func<Stream, CancellationToken, Task> writerAsync)
        {
            this.writer = writer;
            this.writerAsync = writerAsync;
        }

        [Obsolete]
        public void SaveTo(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                writer(file);
            }
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

        [Obsolete]
        public void Read(Action<Stream> reader)
        {
            using (var stream = new MemoryStream())
            {
                writer(stream);
                stream.Seek(0, SeekOrigin.Begin);
                reader(stream);
            }
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