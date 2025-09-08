using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public interface IGetNotifiedOfHeartBeats
    {
        public Task HeartBeatReceived(HeartBeatMessage heartBeatMessage, CancellationToken cancellationToken);
    }
}