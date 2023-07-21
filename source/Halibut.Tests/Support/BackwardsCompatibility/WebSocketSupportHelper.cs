using System;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    static class WebSocketSupportHelper
    {
        public static ClientAndServiceTestCase ChangeWebSocketServiceVersionsToStableWebSocketVersion(ClientAndServiceTestCase testCase)
        {
            if (testCase.ServiceConnectionType == ServiceConnectionType.PollingOverWebSocket && testCase.ClientAndServiceTestVersion.ServiceVersion != null)
            {
                var serviceVersion = new Version(testCase.ClientAndServiceTestVersion.ServiceVersion);

                if (serviceVersion < PreviousVersions.StableWebSocketVersion)
                {
                    testCase.ClientAndServiceTestVersion.ServiceVersion = PreviousVersions.StableWebSocketVersion.ToString();
                }
            }

            return testCase;
        }
    }
}
