using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    static class TaskEx
    {
        // ReSharper disable once UnusedParameter.Global
        // Used to explicitly suppress the compiler warning about
        // using the returned value from async operations
        public static void Ignore(this Task task)
        {
        }

        //TODO: remove when we update to 4.6 and can use Task.CompletedTask
        public static readonly Task CompletedTask = GetCompletedTask();

        static Task GetCompletedTask()
        {
#if HAS_ASYNC_LOCAL
            return Task.CompletedTask;
#else
            return Task.FromResult(0);
#endif
        }

        public static Task Run(Func<object, Task> func, object state) => Task.Factory.StartNew(func, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
    }
}
