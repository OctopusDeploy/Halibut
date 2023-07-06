#nullable enable
using System;
using System.Threading;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndService : IDisposable
    {
        HalibutRuntime Octopus { get; }
        PortForwarder? PortForwarder { get; }
        TService CreateClient<TService>();
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken);
        TClientService CreateClient<TService, TClientService>();
        TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
    }
}