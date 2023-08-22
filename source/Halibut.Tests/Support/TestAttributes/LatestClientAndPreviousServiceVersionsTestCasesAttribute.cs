using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestClientAndPreviousServiceVersionsTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public LatestClientAndPreviousServiceVersionsTestCasesAttribute(bool testWebSocket = true, 
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testAsyncClients = true,
            bool testSyncClients = true,
            bool testAsyncServicesAsWell = false // False means only the sync service will be tested.
            ) :
            base(
                typeof(LatestClientAndPreviousServiceVersionsTestCases),
                nameof(LatestClientAndPreviousServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling, testAsyncClients, testSyncClients, testAsyncServicesAsWell })
        {
        }
        
        static class LatestClientAndPreviousServiceVersionsTestCases
        {
            public static IEnumerator<ClientAndServiceTestCase> GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testAsyncClients, bool testSyncClients, bool testAsyncServicesAsWell)
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
                

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] {
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ServiceVersion),
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v4_4_8.ServiceVersion),
                    },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest,
                    serviceAsyncHalibutFeatureTestCases
                );

                return builder.Build().GetEnumerator();
            }
        }
    }
}
