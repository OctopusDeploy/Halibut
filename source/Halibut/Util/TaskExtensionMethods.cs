#nullable enable
using System;
using System.Threading.Tasks;

namespace Halibut.Util
{
    static class TaskExtensionMethods
    {
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
    }
}