using System;
using System.Threading;

namespace Halibut.ServiceModel
{
    public class RequestCancellationTokens : IDisposable
    {
        CancellationTokenSource linkedCancellationTokenSource;

        public RequestCancellationTokens(CancellationToken connectingCancellationToken, CancellationToken inProgressRequestCancellationToken)
        {
            ConnectingCancellationToken = connectingCancellationToken;
            InProgressRequestCancellationToken = inProgressRequestCancellationToken;
        }

        public CancellationToken ConnectingCancellationToken { get; set; }
        public CancellationToken InProgressRequestCancellationToken { get; set; }

        public CancellationToken LinkedCancellationToken => LazyLinkedCancellationToken.Value;

        Lazy<CancellationToken> LazyLinkedCancellationToken => new (() =>
        {
            if (ConnectingCancellationToken == CancellationToken.None && InProgressRequestCancellationToken == CancellationToken.None)
            {
                return CancellationToken.None;
            }

            linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ConnectingCancellationToken, InProgressRequestCancellationToken);

            return linkedCancellationTokenSource.Token;
        });

        public void Dispose()
        {
            linkedCancellationTokenSource?.Dispose();
        }

        public bool CanCancelInProgressRequest()
        {
            return InProgressRequestCancellationToken != CancellationToken.None;
        }
    }
}