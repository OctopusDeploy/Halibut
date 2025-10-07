using System;
using System.Collections;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class PreviousClientAndLatestServiceVersionsTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public PreviousClientAndLatestServiceVersionsTestCasesAttribute(bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true
        ) :
            base(
                typeof(PreviousClientAndLatestServiceVersionsTestCases),
                nameof(PreviousClientAndLatestServiceVersionsTestCases.GetEnumerator),
                new object[] {testWebSocket, testNetworkConditions, testListening, testPolling})
        {
        }

        static class PreviousClientAndLatestServiceVersionsTestCases
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

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[]
                    {
                        ClientAndServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ClientVersion)
                    },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] {NetworkConditionTestCase.NetworkConditionPerfect},
                    PollingQueuesToTest.InMemory
                );

                return builder.Build();
            }
        }
    }
}