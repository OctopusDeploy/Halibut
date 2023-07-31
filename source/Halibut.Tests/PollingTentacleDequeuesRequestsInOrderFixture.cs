using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task QueuedUpRequestsShouldBeDequeuedInOrder(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            PendingRequestQueue pendingRequestQueue = null;
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
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

                using var tmpDir = new TemporaryDirectory();
                var fileStoppingNewRequests = tmpDir.CreateRandomFile();
                
                // Send a request to polling tentacle that wont complete, until we let it complete.
                var lockService = clientAndService.CreateClient<ILockService>();
                var fileThatLetsUsKnowThePollingTentacleIsBusy = tmpDir.RandomFileName();
                var pollingTentacleKeptBusyRequest = Task.Run(() => lockService.WaitForFileToBeDeleted(fileStoppingNewRequests, fileThatLetsUsKnowThePollingTentacleIsBusy));
                await Wait.For(async () =>
                {
                    await pollingTentacleKeptBusyRequest.AwaitIfFaulted();
                    return File.Exists(fileThatLetsUsKnowThePollingTentacleIsBusy);
                }, CancellationToken);
                

                var countingService = clientAndService.CreateClient<ICountingService>();

                var tasks = new List<Task<int>>();
                for (int i = 0; i < 10; i++)
                {
                    var task = Task.Run(() => countingService.Increment());
                    tasks.Add(task);
                    // Wait for the RPC call to get on to the queue before proceeding
                    await Wait.For(async () =>
                    {
                        await task.AwaitIfFaulted();
                        return pendingRequestQueue.Count == i + 1;
                    }, CancellationToken);
                }
                
                // Complete the task tentacle is doing, so it can pick up more work.
                File.Delete(fileStoppingNewRequests);
                await pollingTentacleKeptBusyRequest;

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