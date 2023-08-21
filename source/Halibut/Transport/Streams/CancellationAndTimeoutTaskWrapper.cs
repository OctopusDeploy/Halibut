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
            using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

            var actionTask = action(linkedCancellationTokenSource.Token);
            var cancellationTask = linkedCancellationTokenSource.Token.AsTask<T>();

            try
            {
                var completedTask = await Task.WhenAny(actionTask, cancellationTask);

                if (completedTask == cancellationTask)
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
                        await onActionTaskExceptionAction(e, timeoutCancellationTokenSource.IsCancellationRequested);
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

                if (timeoutCancellationTokenSource.IsCancellationRequested)
                {
                    return getExceptionOnTimeout();
                }

                return null;
            }
        }
    }
}
