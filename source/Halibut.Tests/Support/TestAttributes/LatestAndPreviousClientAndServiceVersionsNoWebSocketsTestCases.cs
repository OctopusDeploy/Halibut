namespace Halibut.Tests.Support.TestAttributes
{
    public class LatestAndPreviousClientAndServiceVersionsNoWebSocketsTestCases : LatestAndPreviousClientAndServiceVersionsTestCases
    {
        protected override ServiceConnectionType[] ServiceConnectionTypesToTest
        {
            get
            {
                return new[]
                {
                    ServiceConnectionType.Polling,
                    ServiceConnectionType.Listening
                };
            }
        }
    }
}
