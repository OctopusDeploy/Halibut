using System;
using System.IO;
using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support.PortForwarding
{
    public static class PortForwardingServiceSentObserver
    {
        public static PortForwarderBuilder WithPortForwarderServiceSentDataObserver(this PortForwarderBuilder portForwarderBuilder, ServiceConnectionType serviceConnectionType, Action<TcpPump, MemoryStream> serviceSentDataCallback)
        {
            if (serviceConnectionType == ServiceConnectionType.Listening) return portForwarderBuilder.WithDataLoggingForListening(serviceSentDataCallback);
            else return portForwarderBuilder.WithDataLoggingForPolling(serviceSentDataCallback);
        }

        private static PortForwarderBuilder WithDataLoggingForPolling(this PortForwarderBuilder portForwarderBuilder, Action<TcpPump, MemoryStream> serviceSentDataCallback)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataClientToOrigin(ServiceSent(serviceSentDataCallback))
                .Build);
        }

        private static PortForwarderBuilder WithDataLoggingForListening(this PortForwarderBuilder portForwarderBuilder, Action<TcpPump, MemoryStream> serviceSentDataCallback)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataOriginToClient(ServiceSent(serviceSentDataCallback))
                .Build);
        }

        private static IDataTransferObserver ServiceSent(Action<TcpPump, MemoryStream> serviceSentDataCallback)
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver(serviceSentDataCallback).Build();
        }
    }
}