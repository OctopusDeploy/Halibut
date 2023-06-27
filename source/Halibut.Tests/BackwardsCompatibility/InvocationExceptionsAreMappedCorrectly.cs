using System;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class InvocationExceptionsAreMappedCorrectly
    {
        [Test]
        public async Task OldInvocationExceptionMessages_AreMappedTo_ServiceInvocationHalibutClientException_Polling()
        {
            using (var clientAndService = await ClientAndPreviousServiceVersionBuilder
                       .WithPollingService()
                       .WithServiceVersion(PreviousServiceVersions.v5_0_429)
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>(se =>
                {
                    se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                    se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                });

                var ex = Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
            }
        }

        [Test]
        public async Task OldInvocationExceptionMessages_AreMappedTo_ServiceInvocationHalibutClientException_Listening()
        {
            using (var clientAndService = await ClientAndPreviousServiceVersionBuilder
                       .WithListeningService()
                       .WithServiceVersion(PreviousServiceVersions.v5_0_429)
                       .Build())
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