using System;

namespace Halibut.Queue.Redis.MessageStorage
{
    /// <summary>
    /// No-operation implementation of IMessageSerialiserAndDataStreamStorageExceptionObserver
    /// that discards all exception notifications. This is used as a default when no specific
    /// exception handling behavior is required.
    /// </summary>
    public class NoOpMessageSerialiserAndDataStreamStorageExceptionObserver : IMessageSerialiserAndDataStreamStorageExceptionObserver
    {
        /// <summary>
        /// Gets a singleton instance of the no-op observer to avoid unnecessary allocations.
        /// </summary>
        public static readonly IMessageSerialiserAndDataStreamStorageExceptionObserver Instance = new NoOpMessageSerialiserAndDataStreamStorageExceptionObserver();

        /// <summary>
        /// Does nothing with the provided exception. All exceptions are ignored.
        /// </summary>
        /// <param name="exception">The exception that occurred (ignored)</param>
        /// <param name="methodName">The method name where the exception occurred (ignored)</param>
        public void OnException(Exception exception, string methodName)
        {
            // No-op: intentionally does nothing
        }
    }
}

