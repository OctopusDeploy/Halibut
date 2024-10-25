using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Util.AsyncEx;
using NUnit.Framework;

namespace Halibut.Tests.Util.AsyncEx
{
    [TestFixture]
    [NonParallelizable]
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
            await task.TimeoutAfter(TimeSpan.FromSeconds(10), CancellationToken.None);
            triggered.Should().Be(true, "the task should have triggered");
        }
        
        [Test]
        public async Task When_TaskDoesNotCompleteWithinTimeout_ThrowsTimeoutException()
        {
            var triggered = false;
            using var cts = new CancellationTokenSource();
            var task = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromDays(1), cts.Token);
                }
                catch (Exception)
                {
                }

                triggered = true;
            });
            var timeWaiting = Stopwatch.StartNew();
            await AssertException.Throws<TimeoutException>(task.TimeoutAfter(TimeSpan.FromMilliseconds(1), CancellationToken.None));
            timeWaiting.Stop();
            timeWaiting.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "we should have stopped waiting on the task when timeout happened");
            
#pragma warning disable VSTHRD103
            cts.Cancel();
#pragma warning restore VSTHRD103
            await task;
            triggered.Should().Be(true, "task should have continued executing in the background");
        }
        
        [Test]
        public async Task When_TaskGetsCancelled_ThrowsTaskCanceledException()
        {
            var triggered = false;
            
            using var taskWillRunUntilThisIsCancelled = new CancellationTokenSource();
            using var ctsForTimeoutAfter = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
#pragma warning disable VSTHRD103
                ctsForTimeoutAfter.Cancel();
#pragma warning restore VSTHRD103

                try
                {
                    await Task.Delay(TimeSpan.FromDays(1), taskWillRunUntilThisIsCancelled.Token);
                }
                catch
                {
                }

                triggered = true;
            });

            await AssertException.Throws<OperationCanceledException>(task.TimeoutAfter(TimeSpan.FromDays(1), ctsForTimeoutAfter.Token));
            triggered.Should().Be(false, "we should have stopped waiting on the task when cancellation happened");
#pragma warning disable VSTHRD103
            taskWillRunUntilThisIsCancelled.Cancel();
#pragma warning restore VSTHRD103
            await task;
            triggered.Should().Be(true, "task should have continued executing in the background (not entirely ideal, but this task is designed to handle non-cancelable tasks)");
        }
        
        [Test]
        public async Task When_TaskThrowsExceptionAfterTimeout_ExceptionsAreObserved()
        {
            var msg = "this task threw an exception after timeout " + Guid.NewGuid().ToString();

            using var cts = new CancellationTokenSource();
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await VerifyNoUnobservedExceptions<TimeoutException>(
                () => Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromDays(1), cts.Token);
                            }
                            catch
                            {
                            }
                            throw new ApplicationException(msg);
                        }),
                    task => task.TimeoutAfter(TimeSpan.FromMilliseconds(1), CancellationToken.None),
                    () => cts.Cancel(),
                    e => e.Message.Equals(msg));
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }
        
        [Test]
        public async Task When_TaskGetsCanceledButStillThrowsExceptionAfterCancellation_ExceptionsAreObserved()
        {
            using var timeoutAfterCts = new CancellationTokenSource();
            using var taskWaitsOnThis = new CancellationTokenSource();
            var msg = "this task threw an exception after timeout " + Guid.NewGuid().ToString();
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await VerifyNoUnobservedExceptions<OperationCanceledException>(
                () => Task.Run(async () =>
                {
                    await Task.Delay(100);
#pragma warning disable VSTHRD103
                    timeoutAfterCts.Cancel();
#pragma warning restore VSTHRD103
                    try
                        {
                            await Task.Delay(TimeSpan.FromDays(1), taskWaitsOnThis.Token);
                        }
                        catch
                        {
                        }

                        throw new ApplicationException(msg);
                    }),
                task => task.TimeoutAfter(TimeSpan.FromDays(1), timeoutAfterCts.Token),
                () => taskWaitsOnThis.Cancel(),
                e => e.Message.Equals(msg)
                );
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }

        static async Task VerifyNoUnobservedExceptions<T>(Func<Task> createTaskToHaveTimeoutAfterCallInvokedOn,
            Func<Task, Task> timeoutAfterCall,
            Action timeoutAfterCallHasFinished,
            Func<Exception, bool> exceptionThrown)
            where T : Exception
        {
            //inspired by https://stackoverflow.com/a/21269145/779192
            var mre = new ManualResetEvent(initialState: false);
            void Subscription(object? s, UnobservedTaskExceptionEventArgs args)
            {
                if (exceptionThrown(args.Exception) || args.Exception.InnerExceptions.Any(exceptionThrown))
                {
                    mre.Set();
                }
            }

            TaskScheduler.UnobservedTaskException += Subscription;
            try
            {
                var backgroundTask = createTaskToHaveTimeoutAfterCallInvokedOn.Invoke();
                await AssertException.Throws<T>(timeoutAfterCall(backgroundTask));

                timeoutAfterCallHasFinished();
                //delay long enough to ensure the task throws its exception
                while (!backgroundTask.IsCompleted)
                {
                    await Task.Delay(1);
                }

                //unobserved task exceptions are thrown from the finalizer
                createTaskToHaveTimeoutAfterCallInvokedOn = null!; // Allow the task to be GC'ed
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