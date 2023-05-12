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
            using (var clientAndService = await ClientAndOldServiceBuilder.Polling().Build())
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
            using (var clientAndService = await ClientAndOldServiceBuilder.Listening().Build())
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