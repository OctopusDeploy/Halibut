using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util.AsyncEx
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Waits for a) the task to complete, b) the timeout to pass or c) the cancellationToken to be triggered.
        /// Wraps it all up so that we dont get UnobservedTaskExceptions 
        /// </summary>
        /// <param name="task">The task to wait on</param>
        /// <param name="timeout">The amount of time to wait until we give up</param>
        /// <param name="cancellationToken">Supports task cancellation</param>
        /// <returns>The task if successful, otherwise a TimeoutException or OperationCanceledException</returns>
        /// <exception cref="TimeoutException">If the timeout gets reached before the task completes</exception>
        /// <exception cref="OperationCanceledException">The task was cancelled via the cancellation token</exception>
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var timeOutTask = Task.Delay(timeout);
            var cancellationTask = cancellationToken.AsTask();
            var timeoutCancellation = new CancellationTokenSource();
            var wrappedTask = AwaitAndSwallowExceptionsWhenCancelled(task, timeoutCancellation.Token);
            var completedTask = await Task.WhenAny(wrappedTask, timeOutTask, cancellationTask);
            if (completedTask == timeOutTask)
            {
                timeoutCancellation.Cancel();
                if (wrappedTask.IsCompleted)
                {
                    await wrappedTask;
                }
                throw new TimeoutException();
            }

            if (completedTask == cancellationTask)
            {
                timeoutCancellation.Cancel();
                throw new OperationCanceledException();
            }
            await wrappedTask;
        }
        
        /// <summary>
        /// Allows us to await the task, but swallow the exception if the timeout has passed
        /// This prevents us from getting UnobservedTaskException
        /// </summary>
        /// <param name="task"></param>
        /// <param name="timeoutCancellation"></param>
        /// <returns></returns>
        static async Task AwaitAndSwallowExceptionsWhenCancelled(Task task, CancellationToken timeoutCancellation)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                if (!timeoutCancellation.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
        
        static Task AsTask(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            return tcs.Task;
        }
    }
}