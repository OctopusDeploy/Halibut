using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientBuilder
    {
        Task<IClient> Build(CancellationToken cancellationToken);
        IClientBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory);
        IClientBuilder WithHalibutLoggingLevel(LogLevel info);
    }
}
