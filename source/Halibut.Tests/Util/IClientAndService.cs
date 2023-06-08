#nullable enable
using System;
using System.Threading;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.Tests.Util
{
    public interface IClientAndService : IDisposable
    {
        IHalibutRuntime Octopus { get; }
        PortForwarder PortForwarder { get; }
        TService CreateClient<TService>();
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint);
        TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken);
        TClientService CreateClient<TService, TClientService>();
        TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint);
    }
}