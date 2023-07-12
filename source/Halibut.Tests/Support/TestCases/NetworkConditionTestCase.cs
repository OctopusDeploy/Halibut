using System;
using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support.TestCases
{
    public class NetworkConditionTestCase
    {
        public static NetworkConditionTestCase NetworkConditionPerfect = new NetworkConditionTestCase(null, "Perfect");

        public static NetworkConditionTestCase NetworkCondition20MsLatency = new NetworkConditionTestCase((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build(), "20ms send delay");

        public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLastByteArrivingLate =
            new NetworkConditionTestCase(
                (i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(1).Build(),
                "20ms send delay with last byte arriving late");

        public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast2BytesArrivingLate =
            new NetworkConditionTestCase(
                (i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(2).Build(),
                "20ms send delay with last 2 bytes arriving late");

        public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast3BytesArrivingLate =
            new NetworkConditionTestCase(
                (i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(3).Build(),
                "20ms send delay with last 3 bytes arriving late");

        public Func<int, ILogger, PortForwarder>? PortForwarderFactory { get; }
        readonly string networkConditionDescription;

        public NetworkConditionTestCase(Func<int, ILogger, PortForwarder> portForwarderFactory, string networkConditionDescription)
        {
            PortForwarderFactory = portForwarderFactory;
            this.networkConditionDescription = networkConditionDescription;
        }

        public override string ToString()
        {
            return $"Network Condition: '{networkConditionDescription}'";
        }
    }
}