using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    public static class ShouldEventually
    {
        /// <summary>
        /// Keeps executing the given task until it completes without throwing an exception or the timeout is reached.
        /// </summary>
        /// <param name="task">The task to execute repeatedly until it succeeds</param>
        /// <param name="timeout">The maximum time to keep retrying</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task that completes when the given task succeeds or throws when timeout is reached</returns>
        public static async Task Eventually(Func<Task> task, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            var stopwatch = Stopwatch.StartNew();
            Exception? lastException = null;
            
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await task();
                    return; // Success!
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    // Short delay between retries
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(20), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout reached
                        break;
                    }
                }
            }
            
            // If we get here, we've timed out
            var timeoutMessage = $"Task did not complete successfully within {timeout.TotalSeconds:F1} seconds (elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s)";
            if (lastException != null)
            {
                throw new TimeoutException($"{timeoutMessage}. Last exception: {lastException.Message}", lastException);
            }
            throw new TimeoutException(timeoutMessage);
        }
        
        /// <summary>
        /// Keeps executing the given action until it completes without throwing an exception or the timeout is reached.
        /// </summary>
        /// <param name="action">The action to execute repeatedly until it succeeds</param>
        /// <param name="timeout">The maximum time to keep retrying</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public static async Task Eventually(Action action, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await Eventually(() =>
            {
                action();
                return Task.CompletedTask;
            }, timeout, cancellationToken);
        }
        
        /// <summary>
        /// Keeps executing the given function until it returns a result without throwing an exception or the timeout is reached.
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="function">The function to execute repeatedly until it succeeds</param>
        /// <param name="timeout">The maximum time to keep retrying</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The result of the function when it succeeds</returns>
        public static async Task<T> Eventually<T>(Func<Task<T>> function, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            T result = default(T)!;
            
            await Eventually(async () =>
            {
                result = await function();
            }, timeout, cancellationToken);
            
            return result;
        }
        
        /// <summary>
        /// Keeps executing the given function until it returns a result without throwing an exception or the timeout is reached.
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="function">The function to execute repeatedly until it succeeds</param>
        /// <param name="timeout">The maximum time to keep retrying</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The result of the function when it succeeds</returns>
        public static async Task<T> Eventually<T>(Func<T> function, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            T result = default(T)!;
            
            await Eventually(() =>
            {
                result = function();
            }, timeout, cancellationToken);
            
            return result;
        }
    }
}