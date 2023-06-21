using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionIsKilledWhileWaitingForTheResponse
    {
        [Test]
        public async Task ToPolling_AResponseShouldBeQuicklyReturned()
        {
            DoSomeActionService doSomeActionService = new DoSomeActionService();
            using (var clientAndService = ClientServiceBuilder.Polling()
                       .WithService<IDoSomeActionService>(() => doSomeActionService)
                       .WithPortForwarding()
                       .Build())
            {
                var svc = clientAndService.CreateClient<IDoSomeActionService>();

                doSomeActionService.ActionDelegate = () => clientAndService.portForwarder.Dispose();

                // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                var killPortForwarderTask = Task.Run(() => svc.Action());
                
                await Task.WhenAny(killPortForwarderTask, Task.Delay(TimeSpan.FromSeconds(10)));
                
                killPortForwarderTask.Status.Should().Be(TaskStatus.Faulted, "We should immediately get an error response.");
            }
        }
    }
}