using System;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class InvocationExceptionsAreMappedCorrectlyFixture : BaseTest
    {
        [Test]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task OldInvocationExceptionMessages_AreMappedTo_ServiceInvocationHalibutClientException(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>(se =>
                {
                    se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                    se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                });

                Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
            }
        }
    }
}
