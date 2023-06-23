using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace Octopus.TestPortForwarder
{
    public class PortForwarderBuilder
    {
        readonly Uri originServer;
        TimeSpan sendDelay = TimeSpan.Zero;
        readonly List<Func<BiDirectionalDataTransferObserver>> observerFactory = new();
        int? listeningPort;
        int numberOfBytesToDelaySending = 0;
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

        /// <summary>
        /// If possible avoid using this as it may not be possible to bind to the given port.
        /// </summary>
        /// <param name="listeningPort"></param>
        /// <returns></returns>
        public PortForwarderBuilder ListenOnPort(int? listeningPort)
        {
            this.listeningPort = listeningPort;
            return this;
        }

        public PortForwarderBuilder WithNumberOfBytesToDelaySending(int numberOfBytesToDelaySending)
        {
            this.numberOfBytesToDelaySending = numberOfBytesToDelaySending;
            return this;
        }

        public Octopus.TestPortForwarder.PortForwarder Build()
        {
            return new Octopus.TestPortForwarder.PortForwarder(originServer, sendDelay, () =>
                {
                    var results = observerFactory.Select(factory => factory()).ToArray();
                    return BiDirectionalDataTransferObserver.Combiner(results);
                },
                numberOfBytesToDelaySending,
                logger,
                listeningPort);
        }
    }
}