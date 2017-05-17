using System;
using System.IO;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class InMemoryDataStreamReceiver : IDataStreamReceiver
    {
        readonly Func<Stream, Task> writer;

        public InMemoryDataStreamReceiver(Func<Stream, Task> writer)
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
            using (var stream = new MemoryStream())
            {
                await writer(stream).ConfigureAwait(false);
                stream.Seek(0, SeekOrigin.Begin);
                await reader(stream).ConfigureAwait(false);
            }
        }
    }
}
