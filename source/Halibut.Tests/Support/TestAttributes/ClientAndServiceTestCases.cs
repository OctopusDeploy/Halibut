using System.Collections;
using System.Collections.Generic;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.Util;

namespace Halibut.Tests.Support.TestAttributes
{
    public abstract class ClientAndServiceTestCases : IEnumerable<ClientAndServiceTestCase>
    {
        protected virtual ServiceConnectionType[] ServiceConnectionTypesToTest 
        {
            get
            {
                return new[]
                {
                    ServiceConnectionType.Polling,
                    ServiceConnectionType.Listening,
// Disabled while these are causing flakey Team City Tests
//#if SUPPORTS_WEB_SOCKET_CLIENT
//                    ServiceConnectionType.PollingOverWebSocket
//#endif
                };
            }
        }

        protected virtual NetworkConditionTestCase[] NetworkConditionTestCases
        {
            get
            {
                return new[]
                {
                    NetworkConditionTestCase.NetworkConditionPerfect,
                    NetworkConditionTestCase.NetworkCondition20MsLatency,
                    NetworkConditionTestCase.NetworkCondition20MsLatencyWithLastByteArrivingLate,
                    NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast2BytesArrivingLate,
                    NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast3BytesArrivingLate
                };
            }
        }
        
        readonly ClientAndServiceTestVersion[] clientServiceTestVersions;

        protected ClientAndServiceTestCases(params ClientAndServiceTestVersion[] clientServiceTestVersions)
        {
            this.clientServiceTestVersions = clientServiceTestVersions;
        }

        public IEnumerator<ClientAndServiceTestCase> GetEnumerator()
        {
            foreach (var clientServiceTestVersion in clientServiceTestVersions)
            {
                foreach (var serviceConnectionType in ServiceConnectionTypesToTest)
                {
                    foreach (var networkConditionTestCase in NetworkConditionTestCases)
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
