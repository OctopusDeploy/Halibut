using System;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestAttributes;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class InvocationExceptionsAreMappedCorrectlyFixture : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task OldInvocationExceptionMessages_AreMappedTo_ServiceInvocationHalibutClientException(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndPreviousServiceVersionBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithServiceVersion(PreviousVersions.v5_0_429.ServiceVersion.ForServiceConnectionType(serviceConnectionType))
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>(se =>
                {
                    se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                    se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                });

                var ex = Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
            }
        }
    }
}
