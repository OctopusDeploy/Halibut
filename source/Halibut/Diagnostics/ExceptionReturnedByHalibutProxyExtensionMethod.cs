using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Exceptions;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Diagnostics
{

    public static class ExceptionReturnedByHalibutProxyExtensionMethod
    {
        /// <summary>
        /// Classifies the exception thrown from a halibut proxy as a network error or not.
        /// In some cases it is not possible to tell if the exception is a network error. 
        /// </summary>
        /// <param name="exception">The exception thrown from a Halibut proxy object/param>
        /// <returns></returns>
        public static HalibutNetworkExceptionType IsNetworkError(this Exception exception)
        {
            if (exception is NoMatchingServiceOrMethodHalibutClientException)
            {
                return HalibutNetworkExceptionType.NotANetworkError;
            }

            if (IsErrorInService(exception))
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

            if (exception is ProxyException)
            {
                if ((exception as ProxyException).CausedByNetworkError) return HalibutNetworkExceptionType.IsNetworkError;
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

            

            return HalibutNetworkExceptionType.UnknownError;
        }

        /// <summary>
        /// Did the exception thrown from a HalibutProxy object one which was caused by the service e.g. Tentacle
        /// throwing an exception?
        /// </summary>
        /// <param name="exception">The exception thrown from a Halibut proxy object/param>
        /// <returns>true the exception indicates an exception was raised within the execution of the remote method</returns>
        public static bool IsErrorInService(this Exception exception)
        {
            if (exception is HalibutClientException)
            {
                // This message is returned when the service e.g. tentacle itself throws an error
                return exception.Message.Contains("System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.");
            }

            return false;
        }
    }
}