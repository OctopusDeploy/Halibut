using System;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IAsyncDisposable
    {
        HalibutRuntime Client { get; }
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>();
    }
}
