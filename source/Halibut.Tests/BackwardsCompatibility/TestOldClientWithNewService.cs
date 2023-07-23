using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class TestOldClientWithNewService : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task SimplePreviousClientTest(ServiceConnectionType serviceConnectionType)
        {
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithClientVersion(PreviousVersions.v5_0_429.ClientVersion.ForServiceConnectionType(serviceConnectionType))
                       .WithEchoServiceService(echoService)
                       .Build(CancellationToken))
            {

                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("hello");
            }

            echoService.SayHelloCallCount.Should().Be(1);
        }
        
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTestExcludingWebSockets))]
        public async Task SimplePreviousClientTestWithLatency(ServiceConnectionType serviceConnectionType)
        {
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithClientVersion(PreviousVersions.v5_0_429.ClientVersion.ForServiceConnectionType(serviceConnectionType))
                       .WithEchoServiceService(echoService)
                       .WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build())
                       .Build(CancellationToken))
            {
                
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("hello");
            }

            echoService.SayHelloCallCount.Should().Be(1);
        }
    }
}
