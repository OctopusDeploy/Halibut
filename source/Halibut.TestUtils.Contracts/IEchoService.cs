using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface IEchoService
    {
        int LongRunningOperation();

        string SayHello(string name);

        bool Crash();

        int CountBytes(DataStream stream);
    }

    public interface IAsyncEchoService
    {
        Task<int> LongRunningOperationAsync(CancellationToken cancellationToken);

        Task<string> SayHelloAsync(string name, CancellationToken cancellationToken);

        Task<bool> CrashAsync(CancellationToken cancellationToken);

        Task<int> CountBytesAsync(DataStream dataStream, CancellationToken cancellationToken);
    }
}
