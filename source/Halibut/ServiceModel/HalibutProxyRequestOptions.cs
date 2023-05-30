

using System.Threading;

namespace Halibut.ServiceModel
{
    public class HalibutProxyRequestOptions
    {
        public CancellationToken? ConnectCancellationToken { get; }

        public HalibutProxyRequestOptions(CancellationToken? connectCancellationToken)
        {
            this.ConnectCancellationToken = connectCancellationToken;
        }
    }
}