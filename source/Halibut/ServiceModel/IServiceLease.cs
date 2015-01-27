using System;

namespace Halibut.ServiceModel
{
    public interface IServiceLease : IDisposable
    {
        object Service { get; }
    }
}