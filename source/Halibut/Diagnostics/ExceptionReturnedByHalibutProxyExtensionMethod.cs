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

            if (exception is HalibutClientException)
            {
                if (exception.Message.Contains("System.IO.EndOfStreamException")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("System.Net.Sockets.SocketException (110): Connection timed out")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("An existing connection was forcibly closed by the remote host.")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("The I/O operation has been aborted because of either a thread exit or an application request")) return HalibutNetworkExceptionType.IsNetworkError;
                if (exception.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.")) return HalibutNetworkExceptionType.IsNetworkError;
            }

            return HalibutNetworkExceptionType.UnknownError;
        }
    }
}