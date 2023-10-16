using System;
using Octopus.TestPortForwarder;
using Serilog;
using Xunit.Abstractions;

namespace Halibut.Tests.Support.TestCases
{
    public class NetworkConditionTestCase : IXunitSerializable
    {
        public static NetworkConditionTestCase[] All => new[]
        {
            NetworkConditionTestCase.NetworkConditionPerfect,
            NetworkConditionTestCase.NetworkCondition20MsLatency,
            NetworkConditionTestCase.NetworkCondition20MsLatencyWithLastByteArrivingLate,
            //NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast2BytesArrivingLate,
            //NetworkConditionTestCase.NetworkCondition20MsLatencyWithLast3BytesArrivingLate
        };

        public static NetworkConditionTestCase NetworkConditionPerfect = 
            new (0, 
                "Perfect", 
                "Perfect");

        public static NetworkConditionTestCase NetworkCondition20MsLatency = 
            new (1, 
                "20ms SendDelay",
                "20msLatency");

        public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLastByteArrivingLate =
            new (2,
                "20ms SendDelay last byte arrives late",
                "20msLatency&LastByteLate");

        //public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast2BytesArrivingLate =
        //    new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(2).Build(),
        //        "20ms send delay with last 2 bytes arriving late");

        //public static NetworkConditionTestCase NetworkCondition20MsLatencyWithLast3BytesArrivingLate =
        //    new ((i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(3).Build(),
        //        "20ms send delay with last 3 bytes arriving late");

        //TODO: Make nicer.
        int factory;
        public Func<int, ILogger, PortForwarder>? PortForwarderFactory
        {
            get
            {
                switch (factory)
                {
                    case 1:
                        return (i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).Build();
                    case 2:
                        return (i, logger) => PortForwarderBuilder.ForwardingToLocalPort(i, logger).WithSendDelay(TimeSpan.FromMilliseconds(20)).WithNumberOfBytesToDelaySending(1).Build();
                }

                return null;
            }
        }

        public string NetworkConditionDescription { get; private set; }
        
        public string ShortNetworkConditionDescription { get; private set; }

        public NetworkConditionTestCase()
        {
            
        }

        public NetworkConditionTestCase(int factory, string networkConditionDescription, string shortNetworkConditionDescription)
        {
            this.factory = factory;

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

        public void Deserialize(IXunitSerializationInfo info)
        {
            factory = info.GetValue<int>(nameof(factory));
            NetworkConditionDescription = info.GetValue<string>(nameof(NetworkConditionDescription));
            ShortNetworkConditionDescription = info.GetValue<string>(nameof(ShortNetworkConditionDescription));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(factory), factory);
            info.AddValue(nameof(NetworkConditionDescription), NetworkConditionDescription);
            info.AddValue(nameof(ShortNetworkConditionDescription), ShortNetworkConditionDescription);
        }
    }
}