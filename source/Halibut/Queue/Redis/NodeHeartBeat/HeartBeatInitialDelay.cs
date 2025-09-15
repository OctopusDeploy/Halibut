using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class HeartBeatInitialDelay
    {
        public HeartBeatInitialDelay(TimeSpan initialDelay)
        {
            InitialDelay = initialDelay;
        }

        public TimeSpan InitialDelay { get; }

        public async Task WaitBeforeHeartBeatSendingOrReceiving(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(InitialDelay, cancellationToken);
            }
            catch
            {
                // If only Delay had an option to not throw.
            }
        }
    }
}