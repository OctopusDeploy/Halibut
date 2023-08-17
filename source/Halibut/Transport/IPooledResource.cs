using System;

namespace Halibut.Transport
{
    public interface IPooledResource : IDisposable, IAsyncDisposable
    {
        void NotifyUsed();
        bool HasExpired();
    }
}