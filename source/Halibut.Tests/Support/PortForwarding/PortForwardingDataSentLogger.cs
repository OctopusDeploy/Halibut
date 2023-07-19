using Octopus.TestPortForwarder;
using Serilog;

namespace Halibut.Tests.Support.PortForwarding
{
    public static class PortForwarderDataSentLogger
    {
        public static PortForwarderBuilder WithPortForwarderDataLogging(this PortForwarderBuilder portForwarderBuilder, ServiceConnectionType serviceConnectionType)
        {
            if (serviceConnectionType == ServiceConnectionType.Listening) return portForwarderBuilder.WithDataLoggingForListening();
            else return portForwarderBuilder.WithDataLoggingForPolling();
        }

        private static PortForwarderBuilder WithDataLoggingForPolling(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataClientToOrigin(ServiceSent(logger))
                .ObserveDataOriginToClient(ClientSent(logger))
                .Build);
        }

        private static PortForwarderBuilder WithDataLoggingForListening(this PortForwarderBuilder portForwarderBuilder)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PortForwarder>();
            return portForwarderBuilder.WithDataObserver(new BiDirectionalDataTransferObserverBuilder()
                .ObserveDataOriginToClient(ServiceSent(logger))
                .ObserveDataClientToOrigin(ClientSent(logger))
                .Build);
        }

        /// <summary>
        /// Client in this sense means the thing talking to Tentacle e.g. Octopus.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static IDataTransferObserver ClientSent(ILogger logger)
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                logger.Information("Client sent {Count} bytes", stream.Length);
            }).Build();
        }

        private static IDataTransferObserver ServiceSent(ILogger logger)
        {
            return new DataTransferObserverBuilder().WithWritingDataObserver((tcpPump, stream) =>
            {
                logger.Information("Service sent {Count} bytes", stream.Length);
            }).Build();
        }
    }
}