using System;

namespace Halibut.Exceptions
{
    /// <summary>
    /// Raised when a request was not sent to a polling client, because the client presented a different
    /// certificate to the one the request's destination expects. The request was never received by the client.
    /// </summary>
    public class PollingClientCertificateMismatchHalibutClientException : HalibutClientException
    {
        public PollingClientCertificateMismatchHalibutClientException(string message) : base(message)
        {
        }

        public PollingClientCertificateMismatchHalibutClientException(string message, Exception inner) : base(message, inner)
        {
        }

        public PollingClientCertificateMismatchHalibutClientException(string message, string serverException) : base(message, serverException)
        {
        }
    }
}
