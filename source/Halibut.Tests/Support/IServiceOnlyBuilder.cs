using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IServiceOnlyBuilder
    {
        Task<IServiceOnly> Build(CancellationToken cancellationToken);
        IServiceOnlyBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory);
    }
}
