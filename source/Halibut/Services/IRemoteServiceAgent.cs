using System;

namespace Halibut.Services
{
    public interface IRemoteServiceAgent : IDisposable
    {
        bool ProcessNext();
    }
}