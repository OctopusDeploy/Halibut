using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Support.BackwardsCompatibility;
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
            ServiceConnectionType[] serviceConnectionTypes,
            NetworkConditionTestCase[] networkConditionTestCases)
        {
            this.clientServiceTestVersions = clientServiceTestVersions;
            this.serviceConnectionTypes = serviceConnectionTypes;
            this.networkConditionTestCases = networkConditionTestCases;
        }

        public IEnumerable<ClientAndServiceTestCase> Build()
        {
            foreach (var (clientVersion, serviceVersion) in clientServiceTestVersions.Select(x => (x.ClientVersion, x.ServiceVersion)))
            {
                foreach (var serviceConnectionType in serviceConnectionTypes)
                {
                    var clientServiceTestVersion = GetStableClientAndServiceVersionForServiceType(serviceConnectionType, clientVersion, serviceVersion);

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

        ClientAndServiceTestVersion GetStableClientAndServiceVersionForServiceType(ServiceConnectionType serviceConnectionType, Version? clientVersion, Version? serviceVersion)
        {
            if (clientVersion != null)
            {
                return ClientAndServiceTestVersion.ClientOfVersion(clientVersion);
            }

            if (serviceVersion != null)
            {
                return serviceConnectionType switch
                {
                    ServiceConnectionType.PollingOverWebSocket => ClientAndServiceTestVersion.ServiceOfVersion(BumpToStableWebSocketVersionIfRequired(serviceVersion)!),
                    _ => ClientAndServiceTestVersion.ServiceOfVersion(serviceVersion)
                };
            }

            return ClientAndServiceTestVersion.Latest();

            static Version? BumpToStableWebSocketVersionIfRequired(Version? version)
            {
                if (version != null)
                {
                    if (version < PreviousVersions.StableWebSocketVersion)
                    {
                        return PreviousVersions.StableWebSocketVersion;
                    }
                }

                return version;
            }
        }
    }
}
