using System;
using Halibut.Tests.Support.BackwardsCompatibility;

namespace Halibut.Tests.Support.TestCases
{
    public static class ClientServiceTestVersionExtensionMethods
    {
        public static ClientAndServiceTestVersion BumpVersionToaSafeOneForWebSockets(this ClientAndServiceTestVersion clientAndServiceTestVersion)
        {
            
            if(!clientAndServiceTestVersion.IsPreviousService()) return clientAndServiceTestVersion;

            
            var serviceVersion = new Version(clientAndServiceTestVersion.ServiceVersion!);

            if (serviceVersion < PreviousVersions.StableWebSocketVersion)
            {
                return ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.StableWebSocketVersion.ToString());
            }

            return clientAndServiceTestVersion;
        }
    }
}