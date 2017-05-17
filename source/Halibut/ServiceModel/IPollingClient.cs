using System;
using System.Threading.Tasks;

namespace Halibut.ServiceModel
{
    public interface IPollingClient : IDisposable
    {
        void Start();
        Task Stop();
    }
}