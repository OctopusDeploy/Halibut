using System;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class RequestMessageWithCancellationToken
    {
        public Guid ActivityId { get; }
        public RequestMessageWithCancellationToken(RequestMessage requestMessage, CancellationToken cancellationToken)
        {
            RequestMessage = requestMessage;
            CancellationToken = cancellationToken;
            ActivityId = requestMessage.ActivityId;
        }
        
        public RequestMessageWithCancellationToken(PreparedRequestMessage preparedRequestMessage, CancellationToken cancellationToken, Guid activityId)
        {
            this.PreparedRequestMessage = preparedRequestMessage;
            CancellationToken = cancellationToken;
            ActivityId = activityId;
        }

        public RequestMessage? RequestMessage { get; }
        public PreparedRequestMessage? PreparedRequestMessage { get; }
        public CancellationToken CancellationToken { get; }
    }
}