using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class CapabilitiesResponseV2
    {
        public List<string> SupportedCapabilities { get; }

        public CapabilitiesResponseV2(List<string> supportedCapabilities)
        {
            this.SupportedCapabilities = supportedCapabilities;
        }
    }
}