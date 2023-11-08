using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut
{
    public interface IDataStreamReceiver
    {
        Task SaveToAsync(string filePath, CancellationToken cancellationToken);
        
        Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken);
    }
}