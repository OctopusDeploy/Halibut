using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Halibut.TestUtils.Contracts.Tentacle.Services
{
    public class AsyncCapabilitiesServiceV2 : IAsyncCapabilitiesServiceV2
    {
        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new CapabilitiesResponseV2(new List<string> { nameof(IScriptService), nameof(IFileTransferService), nameof(IScriptServiceV2), nameof(ICapabilitiesServiceV2) });
        }
    }
}
