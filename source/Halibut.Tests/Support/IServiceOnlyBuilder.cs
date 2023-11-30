using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;

namespace Halibut.Tests.Support
{
    public interface IServiceOnlyBuilder
    {
        Task<IServiceOnly> Build(CancellationToken cancellationToken);
        IServiceOnlyBuilder WithHalibutLoggingLevel(LogLevel info);
    }
}
