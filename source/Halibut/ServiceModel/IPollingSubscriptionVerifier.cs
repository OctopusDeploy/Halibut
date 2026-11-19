using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.ServiceModel
{
    /// <summary>
    /// Decides whether a polling client may poll the subscription it names.
    /// </summary>
    public interface IPollingSubscriptionVerifier
    {
        Task<bool> IsExpectedClientAsync(Uri subscriptionId, string clientThumbprint, CancellationToken cancellationToken);
    }
}
