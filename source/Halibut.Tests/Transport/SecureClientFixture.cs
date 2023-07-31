using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Util;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class SecureClientFixture : IDisposable
    {
        ServiceEndPoint endpoint;
        HalibutRuntime tentacle;
        ILog log;

        [SetUp]
        public void SetUp()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            tentacle = new HalibutRuntime(services, Certificates.TentacleListening);
            var tentaclePort = tentacle.Listen();
            tentacle.Trust(Certificates.OctopusPublicThumbprint);
            endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint)
            {
                ConnectionErrorRetryTimeout = TimeSpan.MaxValue
            };
            log = new InMemoryConnectionLog(endpoint.ToString());
        }

        public void Dispose()
        {
            tentacle.Dispose();
        }

        [Test]
        public async Task SecureClientClearsPoolWhenAllConnectionsCorrupt()
        {
            var connectionManager = new ConnectionManager();
            var stream = Substitute.For<IMessageExchangeStream>();
            stream.When(x => x.IdentifyAsClient()).Do(x => throw new ConnectionInitializationFailedException(""));
            for (int i = 0; i < HalibutLimits.RetryCountLimit; i++)
            {
                var connection = Substitute.For<IConnection>();
                connection.Protocol.Returns(new MessageExchangeProtocol(stream, log));
                connectionManager.ReleaseConnection(endpoint, connection);
            }

            var request = new RequestMessage
            {
                Destination = endpoint,
                ServiceName = "IEchoService",
                MethodName = "SayHello",
                Params = new object[] { "Fred" }
            };

            var secureClient = new SecureListeningClient((stream, logger) => GetProtocol(stream, logger), endpoint, Certificates.Octopus, log, connectionManager);
            ResponseMessage response = null!;

#pragma warning disable CS0612
            secureClient.ExecuteTransaction((mep) => response = mep.ExchangeAsClient(request), CancellationToken.None);
#pragma warning restore CS0612

            // The pool should be cleared after the second failure
            stream.Received(2).IdentifyAsClient();
            // And a new valid connection should then be made
            response.Result.Should().Be("Fred...");
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog logger)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder().Build(), AsyncHalibutFeature.Disabled, logger), logger);
        }
    }
}
