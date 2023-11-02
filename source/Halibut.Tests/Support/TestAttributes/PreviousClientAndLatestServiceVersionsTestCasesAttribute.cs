using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using Halibut.Util;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class PreviousClientAndLatestServiceVersionsTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public PreviousClientAndLatestServiceVersionsTestCasesAttribute(bool testWebSocket = true, 
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testAsyncClients = true,
            bool testSyncClients = true,
            bool testAsyncServicesAsWell = false, // False means only the sync service will be tested.
            bool testSyncService = true
            ) :
            base(
                typeof(PreviousClientAndLatestServiceVersionsTestCases),
                nameof(PreviousClientAndLatestServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling, testAsyncClients, testSyncClients, testAsyncServicesAsWell, testSyncService })
        {
        }
        
        static class PreviousClientAndLatestServiceVersionsTestCases
        {
            public static IEnumerable GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testAsyncClients, bool testSyncClients, bool testAsyncServicesAsWell, bool testSyncService)
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
                
                List<ForceClientProxyType> clientProxyTypesToTest = new();

                if (testAsyncClients)
                {
                    clientProxyTypesToTest.Add(ForceClientProxyType.AsyncClient);

                    if (testSyncClients)
                    {
                        clientProxyTypesToTest.Add(ForceClientProxyType.SyncClient);
                    }
                }

                var serviceAsyncHalibutFeatureTestCases = AsyncHalibutFeatureValues.All().ToList();
                if (!testAsyncServicesAsWell)
                {
                    serviceAsyncHalibutFeatureTestCases.Remove(AsyncHalibutFeature.Enabled);
                }
                
                if (!testSyncService)
                {
                    serviceAsyncHalibutFeatureTestCases.Remove(AsyncHalibutFeature.Disabled);
                }
                

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] {
                        ClientAndServiceTestVersion.ClientOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ClientVersion)
                    },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest,
                    serviceAsyncHalibutFeatureTestCases
                );

                return builder.Build();
            }
        }
    }
}
