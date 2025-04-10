﻿using System;
using System.Collections;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestAndPreviousClientAndServiceVersionsTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public LatestAndPreviousClientAndServiceVersionsTestCasesAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true) :
            base(
                typeof(LatestAndPreviousClientAndServiceVersionsTestCases),
                nameof(LatestAndPreviousClientAndServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling})
        {
        }
        
        static class LatestAndPreviousClientAndServiceVersionsTestCases
        {
            public static IEnumerable GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling)
            {
                var serviceConnectionTypes = ServiceConnectionTypes.All.ToList();

                if (!testWebSocket)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.PollingOverWebSocket);
                }

                if (!testListening)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.Listening);
                }

                if (!testPolling)
                {
                    serviceConnectionTypes.Remove(ServiceConnectionType.Polling);
                }

                var skipBackwardsCompatibilityTests = Environment.GetEnvironmentVariable("SKIP_BACKWARDS_COMPATIBILITY_TESTS") == "true";
                
                var builder = new ClientAndServiceTestCasesBuilder(
                    skipBackwardsCompatibilityTests
                        ? new[] { ClientAndServiceTestVersion.Latest() }
                        : new[] {
                            ClientAndServiceTestVersion.Latest(),
                            ClientAndServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ClientVersion),
                            ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ServiceVersion),
                            ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v4_4_8.ServiceVersion),
                        },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect });

                return builder.Build();
            }
        }
    }
}
