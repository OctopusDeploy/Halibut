using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Util;
using Nito.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class CancelOnDisposeCancellationTokenFixture : BaseTest
    {
        [Test]
        public async Task Constructor_NoParameters_ShouldCreateValidToken()
        {
            // Arrange & Act
            await using var cancellationToken = new CancelOnDisposeCancellationToken();

            // Assert
            cancellationToken.Token.Should().NotBeNull();
            cancellationToken.Token.IsCancellationRequested.Should().BeFalse();
        }

        [Test]
        public async Task Constructor_WithSingleToken_ShouldCreateLinkedToken()
        {
            // Arrange
            using var parentCts = new CancellationTokenSource();
            var parentToken = parentCts.Token;

            // Act
            await using var cancellationToken = new CancelOnDisposeCancellationToken(parentToken);

            // Assert
            cancellationToken.Token.Should().NotBeNull();
            cancellationToken.Token.IsCancellationRequested.Should().BeFalse();
            
            // When parent is cancelled, child should also be cancelled
            await parentCts.CancelAsync();
            cancellationToken.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task Constructor_WithMultipleTokens_ShouldCreateLinkedToken()
        {
            // Arrange
            using var parentCts1 = new CancellationTokenSource();
            using var parentCts2 = new CancellationTokenSource();
            var parentToken1 = parentCts1.Token;
            var parentToken2 = parentCts2.Token;

            // Act
            await using var cancellationToken = new CancelOnDisposeCancellationToken(parentToken1, parentToken2);

            // Assert
            cancellationToken.Token.Should().NotBeNull();
            cancellationToken.Token.IsCancellationRequested.Should().BeFalse();
            
            // When any parent is cancelled, child should also be cancelled
            await parentCts1.CancelAsync();
            cancellationToken.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task Token_PropertyAccess_ShouldNotThrowAfterDisposal()
        {
            // Arrange
            var cancellationToken = new CancelOnDisposeCancellationToken();
            var token = cancellationToken.Token;

            // Act
            await cancellationToken.DisposeAsync();

            // Assert - accessing Token property should not throw
            var tokenAfterDispose = cancellationToken.Token;
            tokenAfterDispose.Should().Be(token); // Should be the same token instance
        }

        [Test]
        public async Task CancelAsync_ShouldCancelToken()
        {
            // Arrange
            await using var cancellationToken = new CancelOnDisposeCancellationToken();

            // Act
            await cancellationToken.CancelAsync();

            // Assert
            cancellationToken.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task CancelAfter_ShouldCancelTokenAfterTimeout()
        {
            // Arrange
            await using var cancellationToken = new CancelOnDisposeCancellationToken();
            
            // Act
            cancellationToken.CancelAfter(TimeSpan.FromMilliseconds(200));

            // Assert - token should not be cancelled immediately
            cancellationToken.Token.IsCancellationRequested.Should().BeFalse();
            
            // Wait for timeout
            Thread.Sleep(500);
            cancellationToken.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task AwaitTasksBeforeCTSDispose_ShouldWaitForTasksOnDispose()
        {
            // Arrange
            var taskCompleted = false;
            var cancellationToken = new CancelOnDisposeCancellationToken();
            
            var manualResetEvent = new AsyncManualResetEvent();
            // Act
            cancellationToken.AwaitTasksBeforeCTSDispose(manualResetEvent.WaitAsync(CancellationToken));
            
            // Start disposal (don't await yet)
            var disposeTask = cancellationToken.DisposeAsync();
            
            disposeTask.IsCompleted.Should().BeFalse();
            
            manualResetEvent.Set();
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(1)), Task.Run(async () => await disposeTask));
            await disposeTask;

            // Assert
            taskCompleted.Should().BeTrue();
        }

        [Test]
        public async Task AwaitTasksBeforeCTSDispose_ShouldHandleTaskExceptions()
        {
            // Arrange
            var cancellationToken = new CancelOnDisposeCancellationToken();
            
            var faultyTask = Task.Run(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            });

            // Act
            cancellationToken.AwaitTasksBeforeCTSDispose(faultyTask);
            
            // Assert - dispose should not throw even though the task throws
            await cancellationToken.DisposeAsync();
            
            // Task should be faulted
            faultyTask.IsFaulted.Should().BeTrue();
        }

        [Test]
        public async Task DisposeAsync_ShouldCancelTokenBeforeDispose()
        {
            // Arrange
            var cancellationToken = new CancelOnDisposeCancellationToken();
            var token = cancellationToken.Token;

            // Act
            await cancellationToken.DisposeAsync();

            // Assert
            token.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var cancellationToken = new CancelOnDisposeCancellationToken();

            // Act & Assert - multiple dispose calls should not throw
            await cancellationToken.DisposeAsync();
            await cancellationToken.DisposeAsync();
            await cancellationToken.DisposeAsync();
        }

        [Test]
        public async Task DisposeAsync_WithTasksUsingToken_ShouldWaitForCancellation()
        {
            // Arrange
            var taskCancelled = false;
            var cancellationToken = new CancelOnDisposeCancellationToken();
            
            var taskUsingToken = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000, cancellationToken.Token);
                }
                catch (OperationCanceledException)
                {
                    taskCancelled = true;
                }
            });

            cancellationToken.AwaitTasksBeforeCTSDispose(taskUsingToken);

            // Act
            await cancellationToken.DisposeAsync();

            // Assert
            taskCancelled.Should().BeTrue();
        }

        [Test]
        public async Task DisposeAsync_WithMultipleTasks_ShouldWaitForAllTasks()
        {
            // Arrange
            var task1Completed = false;
            var task2Completed = false;
            var cancellationToken = new CancelOnDisposeCancellationToken();
            
            var task1 = Task.Run(async () =>
            {
                await Task.CompletedTask;
                task1Completed = true;
            });
            
            var task2 = Task.Run(async () =>
            {
                await Task.CompletedTask;
                task2Completed = true;
            });

            cancellationToken.AwaitTasksBeforeCTSDispose(task1, task2);

            // Act
            await cancellationToken.DisposeAsync();

            // Assert
            task1Completed.Should().BeTrue();
            task2Completed.Should().BeTrue();
        }
    }
}
