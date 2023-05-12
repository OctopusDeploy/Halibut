using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class InvocationExceptionsAreMappedCorrectly
    {
        [Test]
        public async Task OldInvocationExceptionMessages_AreMappedTo_ServiceInvocationHalibutClientException()
        {
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);
                using (var foo = await new HalibutTestBinaryRunner().Run(octopusPort))
                {
                    var se = new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                    se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                    se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                    var echo = octopus.CreateClient<IEchoService>(se);

                    var ex = Assert.Throws<ServiceInvocationHalibutClientException>(() => echo.Crash());
                }
            }
        }
    }   
}