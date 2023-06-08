using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.Tests.Util.TcpUtils
{
    public class PortForwarderBuilder
    {
        readonly Uri originServer;
        TimeSpan sendDelay = TimeSpan.Zero;
        private List<Func<BiDirectionalDataTransferObserver>> observerFactory = new();
        private int? listeningPort;

        public PortForwarderBuilder(Uri originServer)
        {
            this.originServer = originServer;
        }

        public static PortForwarderBuilder ForwardingToLocalPort(int localPort)
        {
            return new PortForwarderBuilder(new Uri("https://localhost:" + localPort));
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
            }, listeningPort);
        }
    }
}