using System;

namespace Halibut.Transport
{
    public interface IPooledResource : IAsyncDisposable
    {
        void NotifyUsed();
        bool HasExpired();
    }
}