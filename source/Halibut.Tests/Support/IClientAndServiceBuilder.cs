using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndServiceBuilder
    {
        Task<IClientAndService> Build(CancellationToken cancellationToken);
        IClientAndServiceBuilder WithPortForwarding(Func<int, PortForwarder> func);
        IClientAndServiceBuilder WithProxy();
        IClientAndServiceBuilder WithStandardServices();
        IClientAndServiceBuilder WithTentacleServices();
        IClientAndServiceBuilder WithHalibutLoggingLevel(LogLevel info);
        IClientAndServiceBuilder WithCachingService();
    }
}
