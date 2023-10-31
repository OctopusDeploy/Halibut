using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using NSubstitute;
using NUnit.Framework;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Transport
{
    public class SecureClientFixture : IAsyncDisposable
    {
        ServiceEndPoint endpoint;
        HalibutRuntime tentacle;
        ILog log;

        [SetUp]
        public void SetUp()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            tentacle = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.TentacleListening)
                .WithServiceFactory(services)
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            var tentaclePort = tentacle.Listen();
            tentacle.Trust(Certificates.OctopusPublicThumbprint);
            endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint, tentacle.TimeoutsAndLimits)
            {
                ConnectionErrorRetryTimeout = TimeSpan.MaxValue
            };
            log = new TestContextLogCreator("Client", LogLevel.Info).ToCachingLogFactory().ForEndpoint(endpoint.BaseUri);
        }

        public async ValueTask DisposeAsync()
        {
            await tentacle.DisposeAsync();
        }

        [Test]
        public async Task SecureClientClearsPoolWhenAllConnectionsCorrupt()
        {
            var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

            await using var connectionManager = new ConnectionManagerAsync();
            var stream = Substitute.For<IMessageExchangeStream>();
            stream.IdentifyAsClientAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConnectionInitializationFailedException("")));
            
            for (int i = 0; i < halibutTimeoutsAndLimits.RetryCountLimit; i++)
            {
                var connection = Substitute.For<IConnection>();
                connection.Protocol.Returns(new MessageExchangeProtocol(stream, new NoRpcObserver(), log));

                await connectionManager.ReleaseConnectionAsync(endpoint, connection, CancellationToken.None);
            }

            var request = new RequestMessage
            {
                Destination = endpoint,
                ServiceName = "IEchoService",
                MethodName = "SayHello",
                Params = new object[] { "Fred" }
            };

            var tcpConnectionFactory = new TcpConnectionFactory(Certificates.Octopus, halibutTimeoutsAndLimits, new StreamFactory());
            var secureClient = new SecureListeningClient((s, l)  => GetProtocol(s, l), endpoint, Certificates.Octopus, log, connectionManager, tcpConnectionFactory);
            ResponseMessage response = null!;

            using var requestCancellationTokens = new RequestCancellationTokens(CancellationToken.None, CancellationToken.None);

            await secureClient.ExecuteTransactionAsync(async (mep, ct) => response = await mep.ExchangeAsClientAsync(request, ct), requestCancellationTokens);

            // The pool should be cleared after the second failure
            await stream.Received(2).IdentifyAsClientAsync(Arg.Any<CancellationToken>());
            // And a new valid connection should then be made
            response.Result.Should().Be("Fred...");
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog logger)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder(new LogFactory()).Build(), new HalibutTimeoutsAndLimitsForTestsBuilder().Build(), logger), new NoRpcObserver(), logger);
        }
    }
}
