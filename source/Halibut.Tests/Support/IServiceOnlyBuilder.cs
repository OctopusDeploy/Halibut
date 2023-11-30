using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public interface IServiceOnlyBuilder
    {
        Task<IServiceOnly> Build(CancellationToken cancellationToken);
        IServiceOnlyBuilder WithPortForwarding(Func<int, PortForwarder> func);
        IServiceOnlyBuilder WithStandardServices();
        IServiceOnlyBuilder WithTentacleServices();
        IServiceOnlyBuilder WithHalibutLoggingLevel(LogLevel info);
        IServiceOnlyBuilder WithCachingService();
    }
}
