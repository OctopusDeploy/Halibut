using System.Threading;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Util
{
    public static class PortForwarderBuilderExtensionMethods
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="portForwarderBuilder"></param>
        /// <param name="numberOfBytesBeforePausingAStream">When this many bytes have been received by a single port forwarder TCPPump
        /// that TCP pump will be paused. Other pumps including new ones are not paused.</param>
        /// <returns></returns>
        public static PortForwarderBuilder PauseSingleStreamAfterANumberOfBytesHaveBeenSet(this PortForwarderBuilder portForwarderBuilder, int numberOfBytesBeforePausingAStream)
        {
            
            return portForwarderBuilder.WithDataObserver(() =>
            {
                long count = 0;
                var pauseTcpPumpOnceEnoughDataHasBeenPumped = new DataTransferObserverBuilder()
                    .WithWritingDataObserver(((pump, stream) =>
                    {
                        var current = Interlocked.Add(ref count, stream.Length);
                        if (current > numberOfBytesBeforePausingAStream)
                        {
                            pump.Pause();
                        }
                    }))
                    .Build();
                               
                return new BiDirectionalDataTransferObserver(pauseTcpPumpOnceEnoughDataHasBeenPumped, pauseTcpPumpOnceEnoughDataHasBeenPumped);
            });
        }
    }
}