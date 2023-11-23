using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class RequestMessageWithCancellationToken
    {
        public RequestMessageWithCancellationToken(RequestMessage requestMessage, CancellationToken cancellationToken)
        {
            RequestMessage = requestMessage;
            CancellationToken = cancellationToken;
        }

        public RequestMessage RequestMessage { get; }
        public CancellationToken CancellationToken { get; }
    }
}