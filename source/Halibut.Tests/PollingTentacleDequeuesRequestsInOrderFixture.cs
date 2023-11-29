using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
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
            IPendingRequestQueue ?pendingRequestQueue = null;
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithInstantReconnectPollingRetryPolicy()
                       .WithPendingRequestQueueFactory(logFactory =>
                       {
                           return new FuncPendingRequestQueueFactory(uri =>
                           {
                               pendingRequestQueue = new PendingRequestQueueBuilder()
                                   .WithLog(logFactory.ForEndpoint(uri))
                                   .WithPollingQueueWaitTimeout(TimeSpan.FromSeconds(1))
                                   .Build();
                               return pendingRequestQueue;
                           });
                       })
                       .Build(CancellationToken))
            {
                var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                await echoService.SayHelloAsync("Make sure the pending request queue exists");
                pendingRequestQueue.Should().NotBeNull();

                using var tmpDir = new TemporaryDirectory();
                var fileStoppingNewRequests = tmpDir.CreateRandomFile();

                // Send a request to polling tentacle that wont complete, until we let it complete.
                var lockService = clientAndService.CreateAsyncClient<ILockService, IAsyncClientLockService>();
                var fileThatLetsUsKnowThePollingTentacleIsBusy = tmpDir.RandomFileName();
                var pollingTentacleKeptBusyRequest = Task.Run(async () => await lockService.WaitForFileToBeDeletedAsync(fileStoppingNewRequests, fileThatLetsUsKnowThePollingTentacleIsBusy));
                await Wait.For(async () =>
                {
                    await pollingTentacleKeptBusyRequest.AwaitIfFaulted();
                    return File.Exists(fileThatLetsUsKnowThePollingTentacleIsBusy);
                }, CancellationToken);


                var countingService = clientAndService.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();

                var tasks = new List<Task<int>>();
                for (int i = 0; i < 10; i++)
                {
                    var task = Task.Run(async () => await countingService.IncrementAsync());
                    tasks.Add(task);
                    // Wait for the RPC call to get on to the queue before proceeding
                    await Wait.For(async () =>
                    {
                        await task.AwaitIfFaulted();
                        return pendingRequestQueue!.Count == i + 1;
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
