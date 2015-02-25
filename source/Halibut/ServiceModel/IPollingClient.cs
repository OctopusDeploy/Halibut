using System;

namespace Halibut.ServiceModel
{
    public interface IPollingClient : IDisposable
    {
        void Start();
    }
}