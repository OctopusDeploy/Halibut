using System;

namespace Halibut.Transport
{
    /// <summary>
    /// Thrown when a remote party connects to a listening SecureListener but does not complete the
    /// TLS handshake, for example because it disconnected, was reset, or otherwise bailed out before,
    /// or during, authentication. This is a normal occurrence (e.g. a client giving up on a connection
    /// attempt) rather than an error in Halibut, so it should be logged quietly.
    /// </summary>
    public class RemoteFailedToCompleteTlsHandshakeException : Exception
    {
        public RemoteFailedToCompleteTlsHandshakeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
