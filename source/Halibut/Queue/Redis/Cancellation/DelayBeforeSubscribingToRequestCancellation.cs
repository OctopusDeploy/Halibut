using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.Redis.Cancellation
{
    public class DelayBeforeSubscribingToRequestCancellation
    {
        public DelayBeforeSubscribingToRequestCancellation(TimeSpan delay)
        {
            Delay = delay;
        }

        public TimeSpan Delay { get; }
        
        public async Task WaitBeforeHeartBeatSendingOrReceiving(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Delay, cancellationToken);
            }
            catch
            {
                // If only Delay had an option to not throw.
            }
        }
    }
}