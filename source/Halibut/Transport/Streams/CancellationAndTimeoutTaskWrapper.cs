#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    public class CancellationAndTimeoutTaskWrapper
    {
        public static async Task<T> WrapWithCancellationAndTimeout<T>(
            Func<CancellationToken, Task<T>> action,
            Action onCancellationAction,
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

                    onCancellationAction();

                    ThrowMeaningfulException();
                }

                return await actionTask;
            }
            catch (Exception e)
            {
                ThrowMeaningfulException(e);

                throw;
            }

            void ThrowMeaningfulException(Exception? innerException = null)
            {
                if (timeoutCancellationTokenSource.IsCancellationRequested)
                {
                    throw getExceptionOnTimeout();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"The {methodName} operation was cancelled.", innerException);
                }
            }
        }
    }
}
