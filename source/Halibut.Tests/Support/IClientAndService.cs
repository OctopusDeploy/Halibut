#nullable enable
using System;
using System.Threading;
using Halibut.TestProxy;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IDisposable
    {
        HalibutRuntime Client { get; }
        ServiceEndPoint ServiceEndPoint { get; }
        PortForwarder? PortForwarder { get; }
        HttpProxyService? HttpProxy { get; }
        TService CreateClient<TService>(CancellationToken? cancellationToken = null);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken);
        TClientAndService CreateClient<TService, TClientAndService>();
        TClientAndService CreateClient<TService, TClientAndService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TAsyncClientWithOptions CreateClientWithOptions<TService, TSyncClientWithOptions, TAsyncClientWithOptions>();
        TAsyncClientWithOptions CreateClientWithOptions<TService, TSyncClientWithOptions, TAsyncClientWithOptions>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>();
    }
}
