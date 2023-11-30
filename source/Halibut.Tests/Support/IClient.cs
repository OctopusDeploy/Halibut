using System;

namespace Halibut.Tests.Support
{
    public interface IClient : IAsyncDisposable
    {
        HalibutRuntime Client { get; }
        Uri? PollingUri { get; }
        TAsyncClientService CreateClient<TService, TAsyncClientService>(Uri serviceEndPoint);
        TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>();
        TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
    }
}
