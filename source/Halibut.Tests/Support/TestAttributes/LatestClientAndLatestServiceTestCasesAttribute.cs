using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.TestCases;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    ///     Holds all the standard test cases for testing a latest client with a latest service.
    ///     In this case latest means the code as is, rather than some previously built version of halibut.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestClientAndLatestServiceTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public LatestClientAndLatestServiceTestCasesAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testSyncClients = true,
            bool testAsyncClients = true,
            bool testAsyncServicesAsWell = false, // False means only the sync service will be tested.
            bool testSyncService = true,
            params object[] additionalParameters
            ) :
            base(
                typeof(LatestClientAndLatestServiceTestCases),
                nameof(LatestClientAndLatestServiceTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling, testSyncClients, testAsyncClients, testAsyncServicesAsWell, testSyncService, additionalParameters })
        {
        }

        static class LatestClientAndLatestServiceTestCases
        {
            public static IEnumerable GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testSyncClients, bool testAsyncClients, bool testAsyncServicesAsWell, bool testSyncService, object[] additionalParameters)
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
                    new[] { ClientAndServiceTestVersion.Latest() },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest,
                    serviceAsyncHalibutFeatureTestCases
                );
                
                foreach (var clientAndServiceTestCase in builder.Build())
                {
                    if (additionalParameters.Any())
                    {
                        var clientAndServiceTestCaseWithParameters = CombineTestCaseWithAdditionalParameters(clientAndServiceTestCase, additionalParameters);
                        yield return clientAndServiceTestCaseWithParameters;
                    }
                    else
                    {
                        yield return clientAndServiceTestCase;
                    }
                }
            }

            static object[] CombineTestCaseWithAdditionalParameters(ClientAndServiceTestCase clientAndServiceTestCase, object[] additionalParameters)
            {
                var parameters = new object[1 + additionalParameters.Length];

                parameters[0] = clientAndServiceTestCase;

                Array.Copy(additionalParameters, 0, parameters, 1, additionalParameters.Length);

                return parameters;
            }
        }
    }
}
