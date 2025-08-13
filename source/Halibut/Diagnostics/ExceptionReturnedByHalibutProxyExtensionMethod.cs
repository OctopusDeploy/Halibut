using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Exceptions;
using Halibut.Queue.Redis;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Diagnostics
{
    public enum HalibutRetryableErrorType
    {
        IsRetryable,
        UnknownError,
        NotRetryable
    }
    
    public static class ExceptionReturnedByHalibutProxyExtensionMethod
    {
        public static HalibutRetryableErrorType IsRetryableError(this Exception exception)
        {
            var halibutNetworkExceptionType = IsNetworkError(exception);
            switch (halibutNetworkExceptionType)
            {
                case HalibutNetworkExceptionType.IsNetworkError:
                    return HalibutRetryableErrorType.IsRetryable;
                case HalibutNetworkExceptionType.UnknownError:
                    return HalibutRetryableErrorType.UnknownError;
                case HalibutNetworkExceptionType.NotANetworkError:
                    return HalibutRetryableErrorType.NotRetryable;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
            // TODO: This should be in is retryable but for now it needs to be here to work with tentacle client.
            if (exception is RedisDataLoseHalibutClientException)
            {
                return HalibutNetworkExceptionType.IsNetworkError;
            }

            if (exception is HalibutClientException)
            {
                if (exception.Message.Contains(RedisPendingRequestQueue.RequestAbandonedMessage)) return HalibutNetworkExceptionType.IsNetworkError;
            }
            
            // TODO end

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