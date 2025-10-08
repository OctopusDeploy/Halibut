using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;

namespace Halibut.Tests.Support.TestCases
{
    class ClientAndServiceTestCasesBuilder
    {
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;
        readonly ServiceConnectionType[] serviceConnectionTypes;
        readonly NetworkConditionTestCase[] networkConditionTestCases;
        readonly PollingQueuesToTest pollingQueuesToTest;

        public ClientAndServiceTestCasesBuilder(IEnumerable<ClientAndServiceTestVersion> clientServiceTestVersions,
            IEnumerable<ServiceConnectionType> serviceConnectionTypes,
            IEnumerable<NetworkConditionTestCase> networkConditionTestCases, 
            PollingQueuesToTest pollingQueuesToTest)
        {
            this.clientServiceTestVersions = clientServiceTestVersions.Distinct().ToArray();
            this.serviceConnectionTypes = serviceConnectionTypes.Distinct().ToArray();
            this.networkConditionTestCases = networkConditionTestCases.Distinct().ToArray();
            this.pollingQueuesToTest = pollingQueuesToTest;
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
                foreach (var serviceConnectionType in serviceConnectionTypes)
                {
                    bool shouldTestDifferentQueues = (serviceConnectionType == ServiceConnectionType.Polling) && clientServiceTestVersion.IsLatest();
                    var queueTypes = shouldTestDifferentQueues ? PollingQueueTypes() : null;
                    
                    foreach (var networkConditionTestCase in networkConditionTestCases)
                    {
                        // Slightly bad network conditions e.g. a delay of 20ms can blow out test times especially when running for 2000 iterations.
                        // 15 iterations seems ok.
                        var recommendedIterations = 15;
                        if (networkConditionTestCase == NetworkConditionTestCase.NetworkConditionPerfect)
                        {
                            recommendedIterations = StandardIterationCount.ForServiceType(serviceConnectionType, clientServiceTestVersion);
                        }

                        if (queueTypes != null)
                        {
                            foreach (var pollingQueueTestCase in queueTypes)
                            {
                                cases.Add(new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, pollingQueueTestCase));
                            }
                        }
                        else
                        {
                            cases.Add(new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, null));
                        }
                    }
                }
            }

            var distinct = cases.Distinct().ToList();

            return distinct;
        }

        List<PollingQueueTestCase> PollingQueueTypes()
        {
            // TODO inline these so each if creates and returns the list.
            var queueTypes = new List<PollingQueueTestCase>();
            if(pollingQueuesToTest is PollingQueuesToTest.InMemory or PollingQueuesToTest.All) queueTypes.Add(PollingQueueTestCase.InMemory);
            if (pollingQueuesToTest is PollingQueuesToTest.RedisOnly or PollingQueuesToTest.All)
            {
#if NET8_0_OR_GREATER
                queueTypes.Add(PollingQueueTestCase.Redis);
#endif
            }
            return queueTypes;
        }
    }
}
