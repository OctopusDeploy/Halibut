using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
    public class PendingRequestQueueFixture
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task CanCancelAPendingRequestBeforeItIsDequeued(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";
            var request = new RequestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Destination = new ServiceEndPoint(new Uri(endpoint), "thumbprint")
            };
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var (pendingRequestQueue, task) = async ? await StartQueueAndWaitAsATask(endpoint, request, cancellationTokenSource)
                    : await StartQueueAndWaitAsyncAsATask(endpoint, request, cancellationTokenSource);

            cancellationTokenSource.Cancel();

            Exception actualException = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            // Assert
            actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>();
            var next = await pendingRequestQueue.DequeueAsync();
            next.Should().BeNull();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task CannotCancelAPendingRequestAfterItIsDequeued(bool async)
        {
            // Arrange
            const string endpoint = "poll://endpoint001";
            var request = new RequestMessage
            {
                Id = Guid.NewGuid().ToString(),
                Destination = new ServiceEndPoint(new Uri(endpoint), "thumbprint")
            };
            var expectedResponse = new ResponseMessage
            {
                Id = request.Id,
                Result = new object()
            };
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var (pendingRequestQueue, task) = async ? await StartQueueAndWaitAsATask(endpoint, request, cancellationTokenSource)
                : await StartQueueAndWaitAsyncAsATask(endpoint, request, cancellationTokenSource);

            var dequeued = async ? await pendingRequestQueue.DequeueAsync() : pendingRequestQueue.Dequeue();
            dequeued.Should().NotBeNull().And.Be(request);

            pendingRequestQueue.ApplyResponse(expectedResponse, request.Destination);

            cancellationTokenSource.Cancel();

            var response = await task;

            // Assert
            response.Should().Be(expectedResponse);
            var next = await pendingRequestQueue.DequeueAsync();
            next.Should().BeNull();
        }

        static async Task<(PendingRequestQueue, Task<ResponseMessage> task)> StartQueueAndWaitAsyncAsATask(
            string endpoint,
            RequestMessage request,
            CancellationTokenSource cancellationTokenSource)
        {
            var log = new InMemoryConnectionLog(endpoint);
            var pendingRequestQueue = new PendingRequestQueue(log);

            var task = Task.Run(
                async () => await pendingRequestQueue.QueueAndWaitAsync(request, cancellationTokenSource.Token),
                CancellationToken.None);

            while (pendingRequestQueue.IsEmpty)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
            }

            return (pendingRequestQueue, task);
        }

        static async Task<(PendingRequestQueue, Task<ResponseMessage> task)> StartQueueAndWaitAsATask(
            string endpoint,
            RequestMessage request,
            CancellationTokenSource cancellationTokenSource)
        {
            var log = new InMemoryConnectionLog(endpoint);
            var pendingRequestQueue = new PendingRequestQueue(log);

            var task = Task.Run(
                () => pendingRequestQueue.QueueAndWait(request, cancellationTokenSource.Token),
                CancellationToken.None);

            while (pendingRequestQueue.IsEmpty)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), CancellationToken.None);
            }

            return (pendingRequestQueue, task);
        }
    }
}