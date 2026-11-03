using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Halibut.Tests.Support
{
    /// <summary>
    /// Recognises the narrow class of transport timeouts tracked in LEV-2253: a connection that was
    /// established (and usually authenticated) and then stopped responding mid-exchange.
    ///
    /// This is deliberately much narrower than <see cref="Halibut.Diagnostics.ExceptionReturnedByHalibutProxyExtensionMethod.IsNetworkError"/>:
    /// connection refused, resets, protocol errors, certificate problems and assertion failures are NOT matched.
    /// </summary>
    public static class TransportTimeoutClassifier
    {
        static readonly string[] TimeoutMessages =
        {
            // net48 SocketError.TimedOut (10060), surfaced through IOException / HalibutClientException messages
            "connected party did not properly respond after a period of time",
            "connected host has failed to respond",
            // Linux / macOS
            "Operation timed out",
            "Connection timed out",
            // Polling (incl. WebSocket) pickup timeout, the symptom when the polling connection hangs (LEV-2246)
            "did not collect the request within the allowed time",
        };

        public static bool IsTransportTimeout(Exception? exception)
        {
            foreach (var e in Flatten(exception))
            {
                if (e is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut)
                {
                    return true;
                }

                foreach (var message in TimeoutMessages)
                {
                    if (e.Message.IndexOf(message, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static IEnumerable<Exception> Flatten(Exception? exception)
        {
            if (exception == null) yield break;

            yield return exception;

            if (exception is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    foreach (var e in Flatten(inner)) yield return e;
                }
            }
            else
            {
                foreach (var e in Flatten(exception.InnerException)) yield return e;
            }
        }
    }
}
