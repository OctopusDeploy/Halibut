

using System.Threading;

namespace Halibut.ServiceModel
{
    public class HalibutProxyRequestOptions
    {
        /// <summary>
        /// When cancelled, this will only stop a RPC call if it is known to not be
        /// received by the service.
        /// </summary>
        public CancellationToken? ConnectCancellationToken { get; }

        public HalibutProxyRequestOptions(CancellationToken? connectCancellationToken)
        {
            this.ConnectCancellationToken = connectCancellationToken;
        }
    }
}