using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Halibut.Exceptions;

namespace Halibut.Diagnostics
{
    public static class ExceptionExtensions
    {
        public static Exception UnpackFromContainers(this Exception error)
        {
            var aggregateException = error as AggregateException;
            if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
            {
                return UnpackFromContainers(aggregateException.InnerExceptions[0]);
            }

            if (error is TargetInvocationException && error.InnerException != null)
            {
                return UnpackFromContainers(error.InnerException);
            }

            return error;
        }

        public static bool IsSocketTimeout(this Exception exception)
        {
            return (exception.InnerException as SocketException)?.SocketErrorCode == SocketError.TimedOut;
        }

        public static bool IsSocketConnectionReset(this Exception exception)
        {
            return (exception.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionReset;
        }

        public static bool IsSocketConnectionTimeout(this Exception exception)
        {
            return (exception.InnerException as SocketException)?.SocketErrorCode == SocketError.TimedOut;
        }
    }
}
