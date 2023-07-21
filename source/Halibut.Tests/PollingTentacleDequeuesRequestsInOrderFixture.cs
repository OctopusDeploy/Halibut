using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PollingTentacleDequeuesRequestsInOrderFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task QueuedUpRequestsShouldBeDequeuedInOrder(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            PendingRequestQueue pendingRequestQueue = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithPortForwarding()
                       .WithInstantReconnectPollingRetryPolicy()
                       .WithPendingRequestQueueFactory(logFactory =>
                       {
                           return new FuncPendingRequestQueueFactory(uri =>
                           {
                               pendingRequestQueue = new PendingRequestQueue(logFactory.ForEndpoint(uri), TimeSpan.FromSeconds(1));
                               return pendingRequestQueue;
                           });
                       })
                       .Build(CancellationToken))
            {
                var echoService = clientAndService.CreateClient<IEchoService>();
                echoService.SayHello("Make sure the pending request queue exists");
                pendingRequestQueue.Should().NotBeNull();

                // Kill the connection so requests can pile up
                clientAndService.PortForwarder.EnterKillNewAndExistingConnectionsMode();

                var countingService = clientAndService.CreateClient<ICountingService>();

                var tasks = new List<Task<int>>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(() => countingService.Increment()));
                    // Wait for the RPC call to get on to the queue before proceeding
                    await Wait.For(() => pendingRequestQueue.Count == i + 1, CancellationToken);
                }

                clientAndService.PortForwarder.ReturnToNormalMode();

                var counter = 0;
                foreach (var task in tasks)
                {
                    counter++;
                    (await task).Should().Be(counter, "Since each task should be taken off the queue in order.");
                }
            }
        }
    }
}