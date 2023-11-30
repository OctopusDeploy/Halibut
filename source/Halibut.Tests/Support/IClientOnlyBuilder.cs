using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientOnlyBuilder
    {
        Task<IClientOnly> Build(CancellationToken cancellationToken);
        IClientOnlyBuilder WithPortForwarding(Func<int, PortForwarder> func);
        IClientOnlyBuilder WithHalibutLoggingLevel(LogLevel info);
    }
}
