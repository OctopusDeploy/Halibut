using System;
using System.Collections;
using System.Collections.Generic;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.Util;

namespace Halibut.Tests.Support.TestAttributes
{
    public abstract class ClientAndServiceTestCases : IEnumerable<ClientAndServiceTestCase>
    {
        static readonly ServiceConnectionType[] serviceConnectionTypesToTest =
        {
            ServiceConnectionType.Polling,
            ServiceConnectionType.Listening
        };

        static readonly NetworkConditionTestCase[] networkConditionTestCases =
        {
            NetworkConditionTestCase.NetworkConditionPerfect,
            NetworkConditionTestCase.NetworkCondition20MsLatency,
            NetworkConditionTestCase.NetworkCondition20MsLatencyWithLastByteArrivingLate,
            NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast2BytesArrivingLate,
            NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast3BytesArrivingLate
        };
        
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;

        protected ClientAndServiceTestCases(params ClientAndServiceTestVersion[] clientServiceTestVersions)
        {
            this.clientServiceTestVersions = clientServiceTestVersions;
        }

        public IEnumerator<ClientAndServiceTestCase> GetEnumerator()
        {
            foreach (var clientServiceTestVersion in clientServiceTestVersions)
            {
                foreach (var serviceConnectionType in serviceConnectionTypesToTest)
                {
                    foreach (var networkConditionTestCase in networkConditionTestCases)
                    {
                        // Slightly bad network conditions e.g. a delay of 20ms can blow out test times especially when running for 2000 iterations.
                        // 50 seems ok, resulting in 15s tests.
                        int recommendedIterations = 50;
                        if (networkConditionTestCase == NetworkConditionTestCase.NetworkConditionPerfect) recommendedIterations = StandardIterationCount.ForServiceType(serviceConnectionType);
                        yield return new ClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase, recommendedIterations, clientServiceTestVersion);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
