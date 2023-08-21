using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut
{
    public interface IDataStreamReceiver
    {
        [Obsolete]
        void SaveTo(string filePath);
        Task SaveToAsync(string filePath, CancellationToken cancellationToken);
        
        [Obsolete]
        void Read(Action<Stream> reader);
        Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken);
    }
}