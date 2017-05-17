using System;
using System.IO;
using System.Threading.Tasks;

namespace Halibut
{
    public interface IDataStreamReceiver
    {
        Task SaveTo(string filePath);
        Task Read(Func<Stream, Task> reader);
    }
}