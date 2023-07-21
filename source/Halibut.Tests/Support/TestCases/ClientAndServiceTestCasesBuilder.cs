using System.Collections.Generic;
using Halibut.Tests.Util;

namespace Halibut.Tests.Support.TestCases
{
    class ClientAndServiceTestCasesBuilder
    {
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;
        readonly ServiceConnectionType[] serviceConnectionTypes;
        readonly NetworkConditionTestCase[] networkConditionTestCases;

        public ClientAndServiceTestCasesBuilder(
            ClientAndServiceTestVersion[] clientServiceTestVersions,
            ServiceConnectionType[] ServiceConnectionTypes,
            NetworkConditionTestCase[] networkConditionTestCases)
        {
            this.clientServiceTestVersions = clientServiceTestVersions;
            serviceConnectionTypes = ServiceConnectionTypes;
            this.networkConditionTestCases = networkConditionTestCases;
        }

        public IEnumerable<ClientAndServiceTestCase> Build()
        {
            foreach (var clientServiceTestVersionReadOnly in clientServiceTestVersions)
            {
                var clientServiceTestVersion = clientServiceTestVersionReadOnly;
                foreach (var serviceConnectionType in serviceConnectionTypes)
                {
                    clientServiceTestVersion = BumpVersionIfRequired(serviceConnectionType, clientServiceTestVersion);

                    foreach (var networkConditionTestCase in networkConditionTestCases)
                    {
                        // Slightly bad network conditions e.g. a delay of 20ms can blow out test times especially when running for 2000 iterations.
                        // 15 iterations seems ok.
                        var recommendedIterations = 15;
                        if (networkConditionTestCase == NetworkConditionTestCase.NetworkConditionPerfect)
                        {
                            recommendedIterations = StandardIterationCount.ForServiceType(serviceConnectionType);
                        }

                        yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion);
                    }
                }
            }
        }

        static ClientAndServiceTestVersion BumpVersionIfRequired(ServiceConnectionType serviceConnectionType, ClientAndServiceTestVersion clientServiceTestVersion)
        {
            if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                clientServiceTestVersion = clientServiceTestVersion.BumpVersionToaSafeOneForWebSockets();
            }

            return clientServiceTestVersion;
        }
    }
}
