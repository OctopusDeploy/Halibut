using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestClientAndPreviousServiceVersionsTestCasesAttribute : TestCaseSourceAttribute
    {
        public LatestClientAndPreviousServiceVersionsTestCasesAttribute(bool testWebSocket = true, 
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testAsyncAndSyncClients = true
            ) :
            base(
                typeof(LatestClientAndPreviousServiceVersionsTestCases),
                nameof(LatestClientAndPreviousServiceVersionsTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testAsyncAndSyncClients })
        {
        }
        
        static class LatestClientAndPreviousServiceVersionsTestCases
        {
            public static IEnumerator<ClientAndServiceTestCase> GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testAsyncAndSyncClients)
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
                
                ForceClientProxyType[] clientProxyTypesToTest = new ForceClientProxyType[0];
                if (testAsyncAndSyncClients)
                {
                    clientProxyTypesToTest = ForceClientProxyTypeValues.All;
                }

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] {
                        ClientAndServiceTestVersion.ServiceOfVersion(PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417.ServiceVersion),
                    },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest
                );

                return builder.Build().GetEnumerator();
            }
        }
    }
}
