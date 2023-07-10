using System;
using System.Collections;
using System.Collections.Generic;
using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    ///     Holds all the standard test cases for testing a latest client with a latest service.
    ///     In this case latest means the code as is, rather than some previously built version of halibut.
    /// </summary>
    public class LatestClientAndServiceTestCases : IEnumerable<LatestClientAndServiceTestCase>
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

        public IEnumerator<LatestClientAndServiceTestCase> GetEnumerator()
        {
            foreach (var serviceConnectionType in serviceConnectionTypesToTest)
            {
                foreach (var networkConditionTestCase in networkConditionTestCases)
                {
                    yield return new LatestClientAndServiceTestCase(serviceConnectionType, networkConditionTestCase);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}