using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface IAsyncDoSomeActionService
    {
        Task ActionAsync(CancellationToken cancellationToken);
    }
}