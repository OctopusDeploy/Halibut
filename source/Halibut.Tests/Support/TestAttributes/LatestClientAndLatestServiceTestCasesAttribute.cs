using System;
using System.Collections;
using System.Linq;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    ///     Holds all the standard test cases for testing a latest client with a latest service.
    ///     In this case latest means the code as is, rather than some previously built version of halibut.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class LatestClientAndLatestServiceTestCasesAttribute : TestCaseSourceAttribute
    {
        public LatestClientAndLatestServiceTestCasesAttribute(
            bool testWebSocket = true,
            bool testNetworkConditions = true,
            bool testListening = true,
            bool testPolling = true,
            bool testAsyncAndSyncClients = false, // False while this area of the test infra is being built out.
            params object[] additionalParameters
            ) :
            base(
                typeof(LatestClientAndLatestServiceTestCases),
                nameof(LatestClientAndLatestServiceTestCases.GetEnumerator),
                new object[] { testWebSocket, testNetworkConditions, testListening, testPolling, testAsyncAndSyncClients, additionalParameters })
        {
        }

        static class LatestClientAndLatestServiceTestCases
        {
            public static IEnumerable GetEnumerator(bool testWebSocket, bool testNetworkConditions, bool testListening, bool testPolling, bool testAsyncAndSyncClients, object[] additionalParameters)
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
                
                ForceClientProxyType[] clientProxyTypesToTest = new ForceClientProxyType[0];
                if (testAsyncAndSyncClients)
                {
                    clientProxyTypesToTest = ForceClientProxyTypeValues.All;
                }

                var builder = new ClientAndServiceTestCasesBuilder(
                    new[] { ClientAndServiceTestVersion.Latest() },
                    serviceConnectionTypes.ToArray(),
                    testNetworkConditions ? NetworkConditionTestCase.All : new[] { NetworkConditionTestCase.NetworkConditionPerfect },
                    clientProxyTypesToTest
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
