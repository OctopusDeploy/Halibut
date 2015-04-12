using System;

namespace Halibut
{
    public interface IDataStreamReceiver
    {
        void SaveTo(string filePath);
    }
}