using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Halibut.TestProxy;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IClientAndServiceBuilder
    {
        Task<IClientAndService> Build(CancellationToken cancellationToken);
        IClientAndServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory);
        IClientAndServiceBuilder WithProxy(out Reference<HttpProxyService> proxyService);
        IClientAndServiceBuilder WithStandardServices();
        IClientAndServiceBuilder WithTentacleServices();
        IClientAndServiceBuilder WithHalibutLoggingLevel(LogLevel info);
        IClientAndServiceBuilder WithCachingService();
    }
}
