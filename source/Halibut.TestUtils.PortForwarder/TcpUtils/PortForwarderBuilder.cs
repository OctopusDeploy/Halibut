using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.TestUtils.PortForwarder.TcpUtils;
using Serilog;

namespace Halibut.Tests.Util.TcpUtils
{
    public class PortForwarderBuilder
    {
        readonly Uri originServer;
        TimeSpan sendDelay = TimeSpan.Zero;
        private readonly List<Func<BiDirectionalDataTransferObserver>> observerFactory = new();
        private int? listeningPort;
        readonly ILogger logger;

        public PortForwarderBuilder(Uri originServer, ILogger logger)
        {
            this.originServer = originServer;
            this.logger = logger;
        }

        public static PortForwarderBuilder ForwardingToLocalPort(int localPort, ILogger logger)
        {
            return new PortForwarderBuilder(new Uri("https://localhost:" + localPort), logger);
        }

        public PortForwarderBuilder WithSendDelay(TimeSpan sendDelay)
        {
            this.sendDelay = sendDelay;
            return this;
        }

        public PortForwarderBuilder WithDataObserver(Func<BiDirectionalDataTransferObserver> observerFactory)
        {
            this.observerFactory.Add(observerFactory);
            return this;
        }

        public PortForwarderBuilder ListenOnPort(int? listeningPort)
        {
            this.listeningPort = listeningPort;
            return this;
        }

        public PortForwarder Build()
        {
            return new PortForwarder(originServer, sendDelay, () =>
                {
                    var results = observerFactory.Select(factory => factory()).ToArray();
                    return BiDirectionalDataTransferObserver.Combiner(results);
                },
                logger,
                listeningPort);
        }
    }
}