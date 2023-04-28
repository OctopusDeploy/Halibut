using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Exceptions;
using Halibut.Transport.Protocol;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Diagnostics;

public static class ExceptionReturnedByHalibutProxyExtensionMethod
{
    public static HalibutNetworkExceptionType IsNetworkError(this Exception exception)
    {
        if (exception is NoMatchingServiceOrMethodHalibutClientException)
        {
            return HalibutNetworkExceptionType.NotANetworkError;
        }

        if (IsErrorInTentacle(exception))
        {
            return HalibutNetworkExceptionType.NotANetworkError;
        }

        if (exception is ProtocolException or ProxyException or SocketException or IOException)
        {
            return HalibutNetworkExceptionType.IsNetworkError;
        }

        if (exception is HalibutClientException && exception.InnerException != null)
        {
            return IsNetworkError(exception.InnerException);
        }

        return HalibutNetworkExceptionType.UnknownError;
            
    }
    
    public static bool IsErrorInTentacle(this Exception e) 
    {
        if (e is HalibutClientException)
        {
            // This message is returned when tentacle itself throws an error
            return e.Message.Contains("System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.");
        }

        return false;
    }
}