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
        /// On cancellation, this *will not* cancel the task, it just stops waiting on it. If your task supports
        /// cancellation, you should not use this method.
        /// </summary>
        /// <param name="task">The task to wait on</param>
        /// <param name="timeout">The amount of time to wait until we give up</param>
        /// <param name="cancellationToken">Supports task cancellation</param>
        /// <returns>The task if successful, otherwise a TimeoutException or OperationCanceledException</returns>
        /// <exception cref="TimeoutException">If the timeout gets reached before the task completes or before the task is cancelled</exception>
        /// <exception cref="OperationCanceledException">The task was cancelled via the cancellation token</exception>
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var wrappedTask = AwaitAndSwallowExceptionsWhenTimedOut(task, timeoutTask);
            await Task.WhenAny(wrappedTask, timeoutTask);
            
            cancellationToken.ThrowIfCancellationRequested();
            if (timeoutTask.IsCompleted)
                throw new TimeoutException();

            await wrappedTask;
        }
        
        /// <summary>
        /// Allows us to await the task, but swallow the exception if the timeout has passed
        /// This prevents us from getting UnobservedTaskException
        /// </summary>
        /// <param name="task"></param>
        /// <param name="timeoutCancellation"></param>
        /// <returns></returns>
        static async Task AwaitAndSwallowExceptionsWhenTimedOut(Task task, IAsyncResult timeoutTask)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                if (!timeoutTask.IsCompleted)
                    throw;
            }
        }
    }
}