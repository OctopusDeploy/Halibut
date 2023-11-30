#nullable enable
using System;
using Halibut.TestProxy;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IAsyncDisposable
    {
        HalibutRuntime? Client { get; }
        ServiceEndPoint ServiceEndPoint { get; }
        PortForwarder? PortForwarder { get; }
        HttpProxyService? HttpProxy { get; }
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>();
    }
}
