#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    public delegate Task OnActionTaskException(Exception exception, bool operationTimedOut);
    public delegate Task OnCancellation(Exception cancellationException);

    public class CancellationAndTimeoutTaskWrapper
    {
        public static async Task<T> WrapWithCancellationAndTimeout<T>(
            Func<CancellationToken, Task<T>> action,
            OnCancellation? onCancellationAction,
            OnActionTaskException? onActionTaskExceptionAction,
            Func<Exception> getExceptionOnTimeout,
            TimeSpan timeout,
            string methodName,
            CancellationToken cancellationToken)
        {
            using var cleanupCancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cleanupCancellationTokenSource.Token);

            var timedOut = false;
            var actionTask = action(linkedCancellationTokenSource.Token);
            var timeoutTask = DelayWithoutException.Delay(timeout, linkedCancellationTokenSource.Token);

            try
            {
                var completedTask = await Task.WhenAny(actionTask, timeoutTask);

                timedOut = completedTask == timeoutTask && !linkedCancellationTokenSource.IsCancellationRequested;

                // Ensure we stop the Task.Delay if still running and try and stop the 
                // ActionTask if it supports co-operative cancellation on Timeout
                cleanupCancellationTokenSource.Cancel();
                
                if (completedTask != actionTask)
                {
                    actionTask.IgnoreUnobservedExceptions();

                    var exception = GetMeaningfulException() ?? getExceptionOnTimeout();

                    if (onCancellationAction != null)
                    {
                        await onCancellationAction(exception);
                    }

                    throw exception;
                }

                try
                {
                    return await actionTask;
                }
                catch (Exception e)
                {
                    if (onActionTaskExceptionAction != null)
                    {
                        await onActionTaskExceptionAction(e, timedOut);
                    }

                    throw;
                }
            }
            catch (Exception e)
            {
                ThrowMeaningfulException(e);

                throw;
            }

            void ThrowMeaningfulException(Exception? innerException = null)
            {
                var exception = GetMeaningfulException(innerException);

                if (exception != null)
                {
                    throw exception;
                }
            }

            Exception? GetMeaningfulException(Exception? innerException = null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new OperationCanceledException($"The {methodName} operation was cancelled.", innerException);
                }

                if (timedOut)
                {
                    return getExceptionOnTimeout();
                }

                return null;
            }
        }
    }
}
