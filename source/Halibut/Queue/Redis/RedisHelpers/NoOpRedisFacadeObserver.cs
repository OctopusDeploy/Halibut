using System;
using StackExchange.Redis;

namespace Halibut.Queue.Redis.RedisHelpers
{
    /// <summary>
    /// No-operation implementation of IRedisFacadeObserver that discards all notifications.
    /// This is used as a default when no specific Redis monitoring behavior is required.
    /// </summary>
    public class NoOpRedisFacadeObserver : IRedisFacadeObserver
    {
        /// <summary>
        /// Gets a singleton instance of the no-op observer to avoid unnecessary allocations.
        /// </summary>
        public static readonly IRedisFacadeObserver Instance = new NoOpRedisFacadeObserver();

        /// <summary>
        /// Does nothing with the connection failed notification.
        /// </summary>
        /// <param name="endPoint">The endpoint that failed (ignored)</param>
        /// <param name="failureType">The type of failure (ignored)</param>
        /// <param name="exception">The exception that occurred (ignored)</param>
        public void OnRedisConnectionFailed(string? endPoint, ConnectionFailureType failureType, Exception? exception)
        {
            // No-op: intentionally does nothing
        }

        /// <summary>
        /// Does nothing with the error message notification.
        /// </summary>
        /// <param name="endPoint">The endpoint where the error occurred (ignored)</param>
        /// <param name="message">The error message (ignored)</param>
        public void OnRedisServerRepliedWithAnErrorMessage(string? endPoint, string message)
        {
            // No-op: intentionally does nothing
        }

        /// <summary>
        /// Does nothing with the connection restored notification.
        /// </summary>
        /// <param name="endPoint">The endpoint that was restored (ignored)</param>
        public void OnRedisConnectionRestored(string? endPoint)
        {
            // No-op: intentionally does nothing
        }

        /// <summary>
        /// Does nothing with the retry exception notification.
        /// </summary>
        /// <param name="exception">The exception that occurred (ignored)</param>
        /// <param name="willRetry">Whether the operation will be retried (ignored)</param>
        public void OnRedisOperationFailed(Exception exception, bool willRetry)
        {
            // No-op: intentionally does nothing
        }
    }
}
