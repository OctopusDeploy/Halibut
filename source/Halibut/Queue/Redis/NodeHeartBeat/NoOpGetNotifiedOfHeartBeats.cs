using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class NoOpGetNotifiedOfHeartBeats : IGetNotifiedOfHeartBeats
    {
        public Task HeartBeatReceived(HeartBeatMessage heartBeatMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}