using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class TestOldClientWithNewService
    {
        [Test]
        [TestCaseSource()]
        public async Task Listening()
        {
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndServiceBuilder
                       .WithListeningService()
                       .WithClientVersion(PreviousVersions.v5_0_429)
                       .WithEchoServiceService(echoService)
                       .Build())
            {
                
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("hello");
            }

            echoService.SayHelloCallCount.Should().Be(1);
        }
        
        [Test]
        public async Task Polling()
        {
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndServiceBuilder.WithPollingService().WithClientVersion(PreviousVersions.v5_0_429)
                       .WithEchoServiceService(echoService)
                       .Build())
            {
                
                var echo = clientAndService.CreateClient<IEchoService>();
                echo.SayHello("hello");
            }

            echoService.SayHelloCallCount.Should().Be(1);
        }
    }
}