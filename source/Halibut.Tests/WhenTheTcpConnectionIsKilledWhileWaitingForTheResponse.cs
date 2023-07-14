using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionIsKilledWhileWaitingForTheResponse : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task AResponseShouldBeQuicklyReturned(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithPortForwarding(out var portForwarder)
                       .WithDoSomeActionService(() => portForwarder.Value.Dispose())
                       .Build(CancellationToken))
            {
                var svc = clientAndService.CreateClient<IDoSomeActionService>();

                // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                var killPortForwarderTask = Task.Run(() => svc.Action());

                await Task.WhenAny(killPortForwarderTask, Task.Delay(TimeSpan.FromSeconds(10)));

                killPortForwarderTask.Status.Should().Be(TaskStatus.Faulted, "We should immediately get an error response.");
            }
        }
    }
}