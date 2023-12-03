using System;
using System.Threading;

namespace Halibut.ServiceModel
{
    public class RequestCancellationTokens : IDisposable
    {
        CancellationTokenSource? linkedCancellationTokenSource;

        public RequestCancellationTokens(CancellationToken connectingCancellationToken, CancellationToken inProgressRequestCancellationToken)
        {
            ConnectingCancellationToken = connectingCancellationToken;
            InProgressRequestCancellationToken = inProgressRequestCancellationToken;

            if (ConnectingCancellationToken == CancellationToken.None && InProgressRequestCancellationToken == CancellationToken.None)
            {
                LinkedCancellationToken = CancellationToken.None;
            }
            else if (InProgressRequestCancellationToken == CancellationToken.None)
            {
                LinkedCancellationToken = ConnectingCancellationToken;
            }
            else if (ConnectingCancellationToken == CancellationToken.None)
            {
                LinkedCancellationToken = InProgressRequestCancellationToken;
            }
            else if (ConnectingCancellationToken == InProgressRequestCancellationToken)
            {
                LinkedCancellationToken = ConnectingCancellationToken;
            }
            else
            {
                linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ConnectingCancellationToken, InProgressRequestCancellationToken);

                LinkedCancellationToken = linkedCancellationTokenSource.Token;
            }
        }

        public CancellationToken ConnectingCancellationToken { get; set; }
        public CancellationToken InProgressRequestCancellationToken { get; set; }

        public CancellationToken LinkedCancellationToken { get; private set; }

        public void Dispose()
        {
            LinkedCancellationToken = CancellationToken.None;
            linkedCancellationTokenSource?.Dispose();
            linkedCancellationTokenSource = null;
        }

        public bool CanCancelInProgressRequest()
        {
            return InProgressRequestCancellationToken != CancellationToken.None;
        }
    }
}