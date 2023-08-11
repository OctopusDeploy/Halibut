using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Util;
using Halibut.Util;

namespace Halibut.Tests.Support.TestCases
{
    class ClientAndServiceTestCasesBuilder
    {
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;
        readonly ServiceConnectionType[] serviceConnectionTypes;
        readonly NetworkConditionTestCase[] networkConditionTestCases;
        readonly ForceClientProxyType[] forceClientProxyTypes;
        readonly AsyncHalibutFeature[] serviceAsyncHalibutFeatureTestCases;

        public ClientAndServiceTestCasesBuilder(
            IEnumerable<ClientAndServiceTestVersion> clientServiceTestVersions,
            IEnumerable<ServiceConnectionType> serviceConnectionTypes,
            IEnumerable<NetworkConditionTestCase> networkConditionTestCases,
            IEnumerable<ForceClientProxyType> forceClientProxyTypes,
            IEnumerable<AsyncHalibutFeature> serviceAsyncHalibutFeatureTestCases)
        {
            this.serviceAsyncHalibutFeatureTestCases = serviceAsyncHalibutFeatureTestCases.Distinct().ToArray();
            this.clientServiceTestVersions = clientServiceTestVersions.Distinct().ToArray();
            this.serviceConnectionTypes = serviceConnectionTypes.Distinct().ToArray();
            this.networkConditionTestCases = networkConditionTestCases.Distinct().ToArray();
            this.forceClientProxyTypes = forceClientProxyTypes.Distinct().ToArray();
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

                        foreach (var serviceAsyncHalibutFeatureTestCase in serviceAsyncHalibutFeatureTestCases)
                        {
                            if (clientServiceTestVersion.IsPreviousService() && serviceAsyncHalibutFeatureTestCase == AsyncHalibutFeature.Enabled)
                            {
                                continue;
                            }
                            
                            if (!forceClientProxyTypes.Any())
                            {
                                yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, null, serviceAsyncHalibutFeatureTestCase);
                            }
                            else
                            {
                                if (clientServiceTestVersion.IsPreviousClient())
                                {
                                    yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, null, serviceAsyncHalibutFeatureTestCase);
                                }
                                else
                                {
                                    foreach (var forceClientProxyType in forceClientProxyTypes)
                                    {
                                        yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion, forceClientProxyType, serviceAsyncHalibutFeatureTestCase);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
