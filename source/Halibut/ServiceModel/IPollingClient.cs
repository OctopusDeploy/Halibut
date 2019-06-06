using System;

namespace Halibut.ServiceModel
{
    interface IPollingClient : IDisposable
    {
        void Start();
    }
}