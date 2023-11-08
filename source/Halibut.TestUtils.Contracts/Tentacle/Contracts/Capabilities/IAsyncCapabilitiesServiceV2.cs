using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public interface IAsyncCapabilitiesServiceV2
    {
        public Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken);
    }
}