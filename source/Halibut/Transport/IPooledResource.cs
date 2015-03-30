using System;

namespace Halibut.Transport
{
    public interface IPooledResource : IDisposable
    {
        void NotifyUsed();
        bool HasExpired();
    }
}