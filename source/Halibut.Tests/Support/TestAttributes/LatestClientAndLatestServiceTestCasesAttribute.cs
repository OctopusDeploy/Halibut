using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Halibut.Tests.Support.TestCases;
using Halibut.Util;
using Xunit.Sdk;
using static Halibut.Tests.Support.TestAttributes.LatestClientAndLatestServiceTestCasesAttribute;

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
        
        public static class LatestClientAndLatestServiceTestCases
        {
            //TODO: @server-at-scale - When NUnit is removed, remove this class, and make this method the body of LatestClientAndLatestServiceTestCasesXUnitAttribute.GetData
            public static IEnumerable<object[]> GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testSyncClients, bool testAsyncClients, bool testAsyncServicesAsWell, bool testSyncService, object[] additionalParameters)
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

            static object[] CombineTestCaseWithAdditionalParameters(object[] clientAndServiceTestCase, object[] additionalParameters)
            {
                var parameters = new object[clientAndServiceTestCase.Length + additionalParameters.Length];
                
                Array.Copy(clientAndServiceTestCase, 0, parameters, 0, clientAndServiceTestCase.Length);
                Array.Copy(additionalParameters, 0, parameters, clientAndServiceTestCase.Length, additionalParameters.Length);

                return parameters;
            }
        }
    }

    //TODO: @server-at-scale - When NUnit is removed, replace the above LatestClientAndLatestServiceTestCasesAttribute with this.
    public class LatestClientAndLatestServiceTestCasesXUnitAttribute : DataAttribute
    {
        readonly bool testWebSocket;
        readonly bool testNetworkConditions;
        readonly bool testListening;
        readonly bool testPolling;
        readonly bool testSyncClients;
        readonly bool testAsyncClients;
        readonly bool testAsyncServicesAsWell;
        readonly bool testSyncService;
        readonly object[] additionalParameters;

        public LatestClientAndLatestServiceTestCasesXUnitAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testSyncClients = true,
            bool testAsyncClients = true,
            bool testAsyncServicesAsWell = false, // False means only the sync service will be tested.
            bool testSyncService = true,
            params object[] additionalParameters)
        {
            this.testWebSocket = testWebSocket;
            this.testNetworkConditions = testNetworkConditions;
            this.testListening = testListening;
            this.testPolling = testPolling;
            this.testSyncClients = testSyncClients;
            this.testAsyncClients = testAsyncClients;
            this.testAsyncServicesAsWell = testAsyncServicesAsWell;
            this.testSyncService = testSyncService;
            this.additionalParameters = additionalParameters;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            //TODO: @server-at-scale - When NUnit is removed, move LatestClientAndLatestServiceTestCases.GetEnumerator into here.

            return LatestClientAndLatestServiceTestCases.GetEnumerator(
                testWebSocket,
                testNetworkConditions,
                testListening,
                testPolling,
                testSyncClients,
                testAsyncClients,
                testAsyncServicesAsWell,
                testSyncService,
                additionalParameters);
        }
    }
}
