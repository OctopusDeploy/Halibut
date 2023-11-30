using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;

namespace Halibut.Tests.Support
{
    public interface IClientOnlyBuilder
    {
        Task<IClientOnly> Build(CancellationToken cancellationToken);
        IClientOnlyBuilder WithHalibutLoggingLevel(LogLevel info);
    }
}
