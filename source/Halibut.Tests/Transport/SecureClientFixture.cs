using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
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
        [SyncAndAsync]
        public async Task SecureClientClearsPoolWhenAllConnectionsCorrupt(SyncOrAsync syncOrAsync)
        {
            var connectionManager = new ConnectionManager();
            var stream = Substitute.For<IMessageExchangeStream>();

            syncOrAsync
                .WhenSync(() => stream.When(x => x.IdentifyAsClient()).Do(x => throw new ConnectionInitializationFailedException("")))
                .WhenAsync(() => stream.IdentifyAsClientAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConnectionInitializationFailedException(""))));
            
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

            var secureClient = new SecureListeningClient(GetProtocol, endpoint, Certificates.Octopus, log, connectionManager);
            ResponseMessage response = null!;

#pragma warning disable CS0612
            await syncOrAsync
                .WhenSync(() => secureClient.ExecuteTransaction((mep) => response = mep.ExchangeAsClient(request), CancellationToken.None))
                .WhenAsync(async () => await secureClient.ExecuteTransactionAsync(async (mep, ct) => response = await mep.ExchangeAsClientAsync(request, ct), CancellationToken.None));
#pragma warning restore CS0612

            // The pool should be cleared after the second failure
            await syncOrAsync
                .WhenSync(() => stream.Received(2).IdentifyAsClient())
                .WhenAsync(async () => await stream.Received(2).IdentifyAsClientAsync(Arg.Any<CancellationToken>()));
            // And a new valid connection should then be made
            response.Result.Should().Be("Fred...");
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog logger)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder().Build(), AsyncHalibutFeature.Disabled, logger), logger);
        }
    }
}
