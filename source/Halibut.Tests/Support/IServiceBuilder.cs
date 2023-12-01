using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IServiceBuilder
    {
        Task<IService> Build(CancellationToken cancellationToken);
        IServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory);
    }
}
