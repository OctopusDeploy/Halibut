using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Exceptions;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.Exceptions;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Diagnostics
{
    public static class ExceptionReturnedByHalibutProxyExtensionMethod
    {
        public static HalibutRetryableErrorType IsRetryableError(this Exception exception)
        {
            var halibutNetworkExceptionType = IsNetworkError(exception);
            
            // All network errors can be retried.
            if (halibutNetworkExceptionType == HalibutNetworkExceptionType.IsNetworkError) return HalibutRetryableErrorType.IsRetryable;
            
            if (IsRedisRetryableError(exception)) return HalibutRetryableErrorType.IsRetryable;
            
            if (halibutNetworkExceptionType == HalibutNetworkExceptionType.NotANetworkError) return HalibutRetryableErrorType.NotRetryable;
            
            return HalibutRetryableErrorType.UnknownError;
        }

        static bool IsRedisRetryableError(Exception exception)
        {
            if (exception is RedisDataLoseHalibutClientException 
                || exception is RedisQueueShutdownClientException
                || exception is CouldNotGetDataLossTokenInTimeHalibutClientException
                || exception is ErrorWhilePreparingRequestForQueueHalibutClientException
                || exception is ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueue)
            {
                return true;
            }

            if (exception is HalibutClientException)
            {
                if (exception.Message.Contains("The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.")) return true;
                if (exception.Message.Contains("The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline.")) return true;
                if (exception.Message.Contains("Error occured when reading data from the queue")) return true;
                if (exception.Message.Contains("error occured when preparing request for queue")) return true;
            }
            
            if (exception is HalibutClientException && exception.InnerException != null)
            {
                return IsRedisRetryableError(exception.InnerException);
            }

            return false;
        }

        /// <summary>
        ///     Classifies the exception thrown from a halibut proxy as a network error or not.
        ///     In some cases it is not possible to tell if the exception is a network error.
        /// </summary>
        /// <param name="exception">
        ///     The exception thrown from a Halibut proxy object/param>
        ///     <returns></returns>
        public static HalibutNetworkExceptionType IsNetworkError(this Exception exception)
        {
            if (exception is NoMatchingServiceOrMethodHalibutClientException)
            {
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (exception is ServiceInvocationHalibutClientException)
            {
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (exception is UnexpectedCertificateException
                || exception is FileNotFoundException)
            {
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (exception is ProtocolException
                || exception is SocketException
                || exception is IOException)
            {
                return HalibutNetworkExceptionType.IsNetworkError;
            }

            if (exception is ProxyException proxyException)
            {
                if (proxyException.CausedByNetworkError) return HalibutNetworkExceptionType.IsNetworkError;
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (exception is HalibutClientException && exception.Message.Contains("System.IO.FileNotFoundException"))
            {
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (exception is HalibutClientException && exception.InnerException != null)
            {
                return IsNetworkError(exception.InnerException);
            }

            if (exception is HalibutClientException)
            {
                if (exception.Message.Contains("System.IO.EndOfStreamException")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("System.Net.Sockets.SocketException (110): Connection timed out")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("An existing connection was forcibly closed by the remote host.")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("The I/O operation has been aborted because of either a thread exit or an application request")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("The remote party closed the WebSocket connection without completing the close handshake.")) return HalibutNetworkExceptionType.IsNetworkError;
                
            }

            return HalibutNetworkExceptionType.UnknownError;
        }
    }
}