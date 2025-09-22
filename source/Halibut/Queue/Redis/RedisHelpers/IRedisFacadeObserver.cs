using System;
using StackExchange.Redis;

namespace Halibut.Queue.Redis.RedisHelpers
{
    /// <summary>
    /// Observes events and exceptions that occur within RedisFacade.
    /// This allows monitoring and logging of Redis connection events and retry operations.
    /// </summary>
    public interface IRedisFacadeObserver
    {
        /// <summary>
        /// Called when a Redis connection fails.
        /// </summary>
        /// <param name="endPoint">The endpoint that failed</param>
        /// <param name="failureType">The type of failure</param>
        /// <param name="exception">The exception that occurred, if any</param>
        void OnRedisConnectionFailed(string? endPoint, ConnectionFailureType failureType, Exception? exception);

        /// <summary>
        /// Called when a Redis error message is received.
        /// </summary>
        /// <param name="endPoint">The endpoint where the error occurred</param>
        /// <param name="message">The error message</param>
        void OnRedisServerRepliedWithAnErrorMessage(string? endPoint, string message);

        /// <summary>
        /// Called when a Redis connection is restored.
        /// </summary>
        /// <param name="endPoint">The endpoint that was restored</param>
        void OnRedisConnectionRestored(string? endPoint);

        /// <summary>
        /// When an exception is raised trying to do an operation with redis, this method is called.
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="willRetry">True if the operation will be retried, false if it will fail</param>
        void OnRedisOperationFailed(Exception exception, bool willRetry);
    }
}
