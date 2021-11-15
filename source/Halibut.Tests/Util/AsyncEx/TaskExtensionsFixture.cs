using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Util.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Util.AsyncEx
{
    [TestFixture]
    public class TaskExtensionsFixture
    {
        [Test]
        public async Task When_TaskCompletesWithinTimeout_TaskCompletesSuccessfully()
        {
            var triggered = false;
            var task = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                triggered = true;
            });
            await task.TimeoutAfter(TimeSpan.FromMilliseconds(300), CancellationToken.None);
            triggered.Should().Be(true, "the task should have triggered");
        }
        
        [Test]
        public async Task When_TaskDoesNotCompleteWithinTimeout_ThrowsTimeoutException()
        {
            var triggered = false;
            var task = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                triggered = true;
            });
            Func<Task> act = async () => await task.TimeoutAfter(TimeSpan.FromMilliseconds(100), CancellationToken.None);
            await act.Should().ThrowAsync<TimeoutException>();
            triggered.Should().Be(false, "we should have stopped waiting on the task when timeout happened");
            await Task.Delay(200);
            triggered.Should().Be(true, "task should have continued executing in the background");
        }
        
        [Test]
        public async Task When_TaskGetsCancelled_ThrowsTaskCanceledException()
        {
            var triggered = false;
            var cancellationTokenSource = new CancellationTokenSource();

#pragma warning disable 4014 
            // [CS4014] Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                cancellationTokenSource.Cancel();
            });
            
            var task = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                triggered = true;
            });
#pragma warning restore 4014
            Func<Task> act = async () => await task.TimeoutAfter(TimeSpan.FromMilliseconds(150), cancellationTokenSource.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
            triggered.Should().Be(false, "we should have stopped waiting on the task when cancellation happened");
            await Task.Delay(200);
            triggered.Should().Be(true, "task should have continued executing in the background (not entirely ideal, but this task is designed to handle non-cancelable tasks)");
        }
        
        [Test]
        public async Task When_TaskThrowsExceptionAfterTimeout_ExceptionsAreObserved()
        {
            await VerifyNoUnobservedExceptions<TimeoutException>(Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                throw new ApplicationException("this task threw an exception after timeout");
            }).TimeoutAfter(TimeSpan.FromMilliseconds(100), CancellationToken.None));
        }
        
        [Test]
        public async Task When_TaskGetsCanceledButStillThrowsExceptionAfterCancellation_ExceptionsAreObserved()
        {
            var cancellationTokenSource = new CancellationTokenSource();
#pragma warning disable 4014 
            // [CS4014] Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
            Task.Run(async () =>
#pragma warning restore 4014
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                cancellationTokenSource.Cancel();
            });
            await VerifyNoUnobservedExceptions<OperationCanceledException>(Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                throw new ApplicationException("this task threw an exception after timeout");
            }).TimeoutAfter(TimeSpan.FromMilliseconds(150), cancellationTokenSource.Token));
        }

        static async Task VerifyNoUnobservedExceptions<T>(Task task)
            where T : Exception
        {
            //inspired by https://stackoverflow.com/a/21269145/779192
            var mre = new ManualResetEvent(initialState: false);
            void Subscription(object s, UnobservedTaskExceptionEventArgs args) => mre.Set();
            TaskScheduler.UnobservedTaskException += Subscription;
            try
            {
                Func<Task> act = async () => await task;
                await act.Should().ThrowAsync<T>();

                //delay long enough to ensure the task throws its exception
                await Task.Delay(200);

                //unobserved task exceptions are thrown from the finalizer
                task = null; // Allow the task to be GC'ed
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (mre.WaitOne(2000))
                    Assert.Fail("We should not have had an unobserved task exception");
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= Subscription;
            }
        }
    }
}