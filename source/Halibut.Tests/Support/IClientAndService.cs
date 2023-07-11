#nullable enable
using System;
using System.Threading;
using Halibut.TestProxy;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IDisposable
    {
        HalibutRuntime Octopus { get; }
        PortForwarder? PortForwarder { get; }
        HttpProxyService? Proxy { get; }
        TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null);
        TClientService CreateClient<TService, TClientService>();
        TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
    }
}