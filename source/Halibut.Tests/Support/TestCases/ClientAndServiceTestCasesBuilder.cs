using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Util;

namespace Halibut.Tests.Support.TestCases
{
    class ClientAndServiceTestCasesBuilder
    {
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;
        readonly ServiceConnectionType[] serviceConnectionTypes;
        readonly NetworkConditionTestCase[] networkConditionTestCases;

        public ClientAndServiceTestCasesBuilder(
            IEnumerable<ClientAndServiceTestVersion> clientServiceTestVersions,
            IEnumerable<ServiceConnectionType> serviceConnectionTypes,
            IEnumerable<NetworkConditionTestCase> networkConditionTestCases)
        {
            this.clientServiceTestVersions = clientServiceTestVersions.Distinct().ToArray();
            this.serviceConnectionTypes = serviceConnectionTypes.Distinct().ToArray();
            this.networkConditionTestCases = networkConditionTestCases.Distinct().ToArray();
        }

        public IEnumerable<ClientAndServiceTestCase> Build()
        {
            return BuildDistinct();
        }

        List<ClientAndServiceTestCase> BuildDistinct()
        {
            var cases = new List<ClientAndServiceTestCase>();

            foreach (var clientServiceTestVersion in clientServiceTestVersions)
            {
                if(!clientServiceTestVersion.IsLatest()) continue;
                foreach (var serviceConnectionType in serviceConnectionTypes)
                {
                    foreach (var networkConditionTestCase in networkConditionTestCases)
                    {
                        
                        // Slightly bad network conditions e.g. a delay of 20ms can blow out test times especially when running for 2000 iterations.
                        // 15 iterations seems ok.
                        var recommendedIterations = 15;
                        if (networkConditionTestCase == NetworkConditionTestCase.NetworkConditionPerfect)
                        {
                            recommendedIterations = StandardIterationCount.ForServiceType(serviceConnectionType, clientServiceTestVersion);
                        }
                        
                        cases.Add(new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion));
                    }
                }
            }

            var distinct = cases.Distinct().ToList();

            return distinct;
        }
    }
}
