using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests.BackwardsCompatibility
{
    public class TestOldClientWithNewService
    {
        [Test]
        public async Task Listening()
        {
            new SerilogLoggerBuilder().Build().Information("Hello");
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndServiceBuilder.WithListeningService().WithClientVersion(PreviousServiceVersions.v5_0_429)
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
            new SerilogLoggerBuilder().Build().Information("Hello");
            var echoService = new CallRecordingEchoServiceDecorator(new EchoService());
            using (var clientAndService = await PreviousClientVersionAndServiceBuilder.WithPollingService().WithClientVersion(PreviousServiceVersions.v5_0_429)
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