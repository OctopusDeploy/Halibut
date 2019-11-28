using System;

namespace Halibut.Util
{
    class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}