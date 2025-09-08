using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public interface IGetNotifiedOfHeartBeats
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="heartBeatMessage"></param>
        /// <param name="cancellationToken">Request cancelled cancellation token</param>
        /// <returns></returns>
        public Task HeartBeatReceived(HeartBeatMessage heartBeatMessage, CancellationToken cancellationToken);
    }
}