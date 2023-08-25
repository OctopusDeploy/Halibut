using System;
using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support.TestCases
{
    public class NetworkConditionTestCase
    {
        public static NetworkConditionTestCase[] All => new[]
        {
            NetworkConditionTestCase.NetworkConditionPerfect,
            //NetworkConditionTestCase.NetworkCondition20MsLatency,
            //NetworkConditionTestCase.NetworkCondition20MsLatencyWithLastByteArrivingLate,
            //NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast2BytesArrivingLate,
            //NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast3BytesArrivingLate
        };

        public static NetworkConditionTestCase NetworkConditionPerfect = 
            new (null, 
                "Perfect", 
                "Perfect");

        public static NetworkConditionTestCase NetworkCondition20MsLatency = 
            new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build(), 
                "20ms SendDelay",
                "20msLatency");

        public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLastByteArrivingLate =
            new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(1).Build(),
                "20ms SendDelay last byte arrives late",
                "20msLatency&LastByteLate");

        //public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast2BytesArrivingLate =
        //    new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(2).Build(),
        //        "20ms send delay with last 2 bytes arriving late");

        //public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast3BytesArrivingLate =
        //    new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(3).Build(),
        //        "20ms send delay with last 3 bytes arriving late");

        public Func<int, ILogger, PortForwarder>? PortForwarderFactory { get; }

        public string NetworkConditionDescription { get; }
        
        public string ShortNetworkConditionDescription { get; }

        public NetworkConditionTestCase(Func<int, ILogger, PortForwarder>? portForwarderFactory, string networkConditionDescription, string shortNetworkConditionDescription)
        {
            PortForwarderFactory = portForwarderFactory;

            NetworkConditionDescription = networkConditionDescription;
            ShortNetworkConditionDescription = shortNetworkConditionDescription;
        }

        public override string ToString()
        {
            return $"Network: '{NetworkConditionDescription}'";
        }
        
        public string ToShortString()
        {
            return $"Net: '{ShortNetworkConditionDescription}'";
        }
    }
}