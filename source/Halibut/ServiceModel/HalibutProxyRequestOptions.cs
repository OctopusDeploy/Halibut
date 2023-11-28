

using System.Threading;

namespace Halibut.ServiceModel
{
    public class HalibutProxyRequestOptions
    {
        /// <summary>
        /// When cancelled, will only stop an RPC call if it is known to not be received by the service.
        /// For a Listening Service, cancellation can occur when the Client is still connecting to the Service.
        /// For a Polling Service, cancellation can occur when the Client has queued a Request but the Service has not yet Dequeued it.
        /// </summary>
        public CancellationToken? ConnectingCancellationToken { get; }

        /// <summary>
        /// When cancelled, will try to cancel an in-progress / in-flight RPC call.
        /// This is a best effort cancellation and is not guaranteed.
        /// For Sync Halibut, providing this cancellation token is not supported.
        /// For Async Halibut this will attempt to cancel the RPC call.
        /// If the call is to a Listening Service, then cancellation is performed all the way down to the Socket operations.
        /// if the call is to a Polling Service, then cancellation is performed all the way down to the Polling Queue,
        /// this means the client can cancel the call but the service will still process the request and return a response.
        /// </summary>
        public CancellationToken? InProgressRequestCancellationToken { get; }

        public HalibutProxyRequestOptions(CancellationToken? connectingCancellationToken, 
            CancellationToken? inProgressRequestCancellationToken)
        {
            ConnectingCancellationToken = connectingCancellationToken;
            InProgressRequestCancellationToken = inProgressRequestCancellationToken;
        }
    }
}