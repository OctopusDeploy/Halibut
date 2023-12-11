using System.Threading;

namespace Halibut.ServiceModel
{
    public class HalibutProxyRequestOptions
    {
        /// <summary>
        /// When cancelled, will cancel a connecting or in-progress/in-flight RPC call.
        /// </summary>
        public CancellationToken RequestCancellationToken { get; }

        public HalibutProxyRequestOptions(CancellationToken cancellationToken)
        {
            RequestCancellationToken = cancellationToken;
        }
    }
}