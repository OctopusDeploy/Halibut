using Octopus.Tentacle.Contracts.Capabilities;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateCapabilitiesServiceV2 : ICapabilitiesServiceV2
    {
        readonly ICapabilitiesServiceV2 service;

        public DelegateCapabilitiesServiceV2(ICapabilitiesServiceV2 service)
        {
            this.service = service;
        }

        public CapabilitiesResponseV2 GetCapabilities()
        {
            return service.GetCapabilities();
        }
    }
}
