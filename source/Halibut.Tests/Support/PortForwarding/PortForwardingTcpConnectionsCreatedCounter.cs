using System;
using System.IO;
using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support.PortForwarding
{
    public static class PortForwardingTcpConnectionsCreatedCounter
    {
        public static PortForwarderBuilder WithCountTcpConnectionsCreated(this PortForwarderBuilder portForwarderBuilder, out TcpConnectionsCreatedCounter tcpConnectionsCreatedCounter)
        {
            
            var myTcpConnectionsCreatedCounter = new TcpConnectionsCreatedCounter();
            tcpConnectionsCreatedCounter = myTcpConnectionsCreatedCounter;
            
            return portForwarderBuilder.WithDataObserver(() =>
            {
                myTcpConnectionsCreatedCounter.ConnectionsCreatedCount++;
                return new BiDirectionalDataTransferObserverBuilder().Build();
            });
        }
    }
    
    public class TcpConnectionsCreatedCounter
    {
        public long ConnectionsCreatedCount { get; set; } = 0;
    }
}