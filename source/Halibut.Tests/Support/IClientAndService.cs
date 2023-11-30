using System;
using Halibut.TestProxy;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IAsyncDisposable
    {
        HalibutRuntime Client { get; }
        HttpProxyService? HttpProxy { get; }
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>();
    }
}
