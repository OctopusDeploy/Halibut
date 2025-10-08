using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PollingTentacleDequeuesRequestsInOrderFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task QueuedUpRequestsShouldBeDequeuedInOrder(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            halibutTimeoutsAndLimits.PollingQueueWaitTimeout = TimeSpan.FromSeconds(1);
            IPendingRequestQueue? pendingRequestQueue = null;
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithInstantReconnectPollingRetryPolicy()
                       .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                       .WithPendingRequestQueueFactoryBuilder(builder => builder.WithDecorator((_, inner) => inner.CaptureCreatedQueues(queue =>
                       {
                           pendingRequestQueue = queue;
                       })))
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
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                    await pollingTentacleKeptBusyRequest.AwaitIfFaulted();
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
                    return File.Exists(fileThatLetsUsKnowThePollingTentacleIsBusy);
                }, CancellationToken);


                var countingService = clientAndService.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
                
                // The queues don't all work the same with the Count operator, this account for that.
                int baseCount = pendingRequestQueue!.Count;
                
                var tasks = new List<Task<int>>();
                for (int i = 0; i < 10; i++)
                {
                    
                    var task = Task.Run(async () => await countingService.IncrementAsync());
                    tasks.Add(task);
                    // Wait for the RPC call to get on to the queue before proceeding
                    await Wait.For(async () =>
                    {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                        await task.AwaitIfFaulted();
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
                        
                        return pendingRequestQueue!.Count - baseCount == i + 1;
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
