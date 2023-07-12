using Halibut.Tests.Support.TestCases;

namespace Halibut.Tests.Support.TestAttributes
{
    public class LatestAndPreviousClientAndServiceVersionsNoWebSocketPerfectNetworkTestCases : LatestAndPreviousClientAndServiceVersionsNoWebSocketsTestCases
    {
        protected override NetworkConditionTestCase[] NetworkConditionTestCases
        {
            get
            {
                return new[]
                {
                    NetworkConditionTestCase.NetworkConditionPerfect
                };
            }
        }
    }
}
