#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    static class TaskExtensionMethods
    {
#if NETFRAMEWORK
        // Polyfills for Task.WaitAsync(CancellationToken), which is built in from .NET 6.
        // On modern .NET the instance methods take precedence over these extension methods.
        // As with the built-in versions, cancellation stops waiting on the task; it does not cancel the task itself.
        public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            await WaitForCompletionOrCancellation(task, cancellationToken).ConfigureAwait(false);
            await task.ConfigureAwait(false);
        }

        public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            await WaitForCompletionOrCancellation(task, cancellationToken).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        static async Task WaitForCompletionOrCancellation(Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled) return;
            cancellationToken.ThrowIfCancellationRequested();

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                var completed = await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false);
                if (completed != task) throw new OperationCanceledException(cancellationToken);
            }
        }
#endif


        public static void IgnoreUnobservedExceptions(this Task task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var observedException = task.Exception;
                }

                return;
            }

            task.ContinueWith(
                t =>
                {
                    var observedException = t.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }


        public static IAsyncResult AsAsynchronousProgrammingModel<T>(
            this Task<T> task,
            AsyncCallback? callback,
            object? state)
        {
            // Sourced from https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/interop-with-other-asynchronous-patterns-and-types
            var tcs = new TaskCompletionSource<T>(state);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    tcs.TrySetException(t.Exception!.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    tcs.TrySetResult(t.Result);
                }

                callback?.Invoke(tcs.Task);
            }, TaskScheduler.Default);

            return tcs.Task;
        }

        public static IAsyncResult AsAsynchronousProgrammingModel(
            this Task task,
            AsyncCallback? callback,
            object? state)
        {
            // Sourced from https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/interop-with-other-asynchronous-patterns-and-types
            var tcs = new TaskCompletionSource<object>(state);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    tcs.TrySetException(t.Exception!.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    tcs.TrySetResult(new object());
                }
                
                callback?.Invoke(tcs.Task);
                
            }, TaskScheduler.Default);

            return tcs.Task;
        }
    }
}