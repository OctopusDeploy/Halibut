using System;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientCapabilitiesServiceV2
    {
        public Task<CapabilitiesResponseV2> GetCapabilitiesAsync();
    }
}
