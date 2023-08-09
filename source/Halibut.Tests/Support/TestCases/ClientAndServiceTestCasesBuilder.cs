using System;
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
        readonly ForceClientProxyType[] forceClientProxyTypes;

        public ClientAndServiceTestCasesBuilder(
            ClientAndServiceTestVersion[] clientServiceTestVersions,
            ServiceConnectionType[] ServiceConnectionTypes,
            NetworkConditionTestCase[] networkConditionTestCases,
            ForceClientProxyType[] forceClientProxyTypes)
        {
            this.clientServiceTestVersions = clientServiceTestVersions;
            serviceConnectionTypes = ServiceConnectionTypes;
            this.networkConditionTestCases = networkConditionTestCases;
            this.forceClientProxyTypes = forceClientProxyTypes;
        }

        public IEnumerable<ClientAndServiceTestCase> Build()
        {
            foreach (var clientServiceTestVersion in clientServiceTestVersions)
            {
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

                        if (!forceClientProxyTypes.Any())
                        {
                            yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, null);
                            Environment.Exit(-1);
                            yield break;
                        }
                        else
                        {
                            if (clientServiceTestVersion.IsPreviousClient())
                            {
                                yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, null);
                                yield break;
                            }
                            else
                            {
                                foreach (var forceClientProxyType in forceClientProxyTypes)
                                {
                                    yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, forceClientProxyType);
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
