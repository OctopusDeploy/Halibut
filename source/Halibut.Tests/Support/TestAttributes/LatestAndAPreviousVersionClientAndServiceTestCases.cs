using System;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.TestAttributes
{
    public class LatestAndAPreviousVersionClientAndServiceTestCases : ClientAndServiceTestCases
    {
        public LatestAndAPreviousVersionClientAndServiceTestCases()
            : base(ClientServiceTestVersion.Latest(),
                ClientServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417),
                ClientServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417))
        {
            
        }
    }
}
