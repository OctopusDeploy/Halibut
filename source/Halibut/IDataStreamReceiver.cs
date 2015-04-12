using System;
using System.IO;

namespace Halibut
{
    public interface IDataStreamReceiver
    {
        void SaveTo(string filePath);
        void Read(Action<Stream> reader);
    }
}