using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenTheTcpConnectionIsKilledWhileWaitingForTheResponse : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task AResponseShouldBeQuicklyReturned(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPortForwarding(out var portForwarder)
                       .WithDoSomeActionService(() => portForwarder.Value.Dispose())
                       .Build(CancellationToken))
            {
                var svc = clientAndService.CreateClient<IDoSomeActionService, IAsyncClientDoSomeActionService>();

                // When svc.Action() is executed, tentacle will kill the TCP connection and dispose the port forwarder preventing new connections.
                var killPortForwarderTask = Task.Run(async () => await svc.ActionAsync());

                await Task.WhenAny(killPortForwarderTask, Task.Delay(TimeSpan.FromSeconds(10)));

                killPortForwarderTask.Status.Should().Be(TaskStatus.Faulted, "We should immediately get an error response.");
            }
        }
    }
}