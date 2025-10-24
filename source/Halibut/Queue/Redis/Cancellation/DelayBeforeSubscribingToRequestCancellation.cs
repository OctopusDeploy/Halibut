using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

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
            await DelayWithoutException.Delay(Delay, cancellationToken);
        }
    }
}