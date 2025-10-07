using System;

namespace Halibut.Queue.Redis.MessageStorage
{
    /// <summary>
    /// Observes exceptions that occur within IMessageSerialiserAndDataStreamStorage implementations.
    /// This allows monitoring and logging of errors during message serialization and data stream operations.
    /// </summary>
    public interface IMessageSerialiserAndDataStreamStorageExceptionObserver
    {
        /// <summary>
        /// Called when an exception occurs in any IMessageSerialiserAndDataStreamStorage method.
        /// Errors caught here are most likely caused by the Redis Pending Request Queue itself.
        /// </summary>
        /// <param name="exception">The exception that was raised</param>
        /// <param name="methodName">The name of the method where the exception occurred</param>
        void OnException(Exception exception, string methodName);
    }
}

