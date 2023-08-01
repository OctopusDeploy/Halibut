using System;
using System.Threading.Tasks;

namespace Halibut.Util.AsyncEx
{
    internal static class TaskCompletionSourceExtensions
    {

        /// <summary>
        /// Attempts to complete a <see cref="TaskCompletionSource"/>, forcing all continuations onto a threadpool thread even if they specified <c>ExecuteSynchronously</c>.
        /// </summary>
        /// <param name="this">The task completion source. May not be <c>null</c>.</param>
        [Obsolete]
        public static void TrySetResultWithBackgroundContinuations(this TaskCompletionSource @this)
        {
            // Set the result on a threadpool thread, so any synchronous continuations will execute in the background.
            Task.Run(() => @this.TrySetResult());

            // Wait for the TCS task to complete; note that the continuations may not be complete.
            @this.Task.Wait();
        }

    }
}
