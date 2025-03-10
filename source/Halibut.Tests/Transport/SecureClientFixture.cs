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
using Halibut.Tests.TestServices;
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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        ServiceEndPoint endpoint;
        HalibutRuntime tentacle;
        ILog log;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [SetUp]
        public void SetUp()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
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
                var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
                var activeConnectionLimiter = new ActiveTcpConnectionsLimiter(limits);
                connection.Protocol.Returns(new MessageExchangeProtocol(stream, limits, activeConnectionLimiter, NoIdentityObserver.Instance, log));

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
            var secureClient = new SecureListeningClient(GetProtocol, endpoint, Certificates.Octopus, log, connectionManager, tcpConnectionFactory);
            ResponseMessage response = null!;

            await secureClient.ExecuteTransactionAsync(async (mep, ct) => response = await mep.ExchangeAsClientAsync(request, ct), CancellationToken.None);

            // The pool should be cleared after the second failure
            await stream.Received(2).IdentifyAsClientAsync(Arg.Any<CancellationToken>());
            // And a new valid connection should then be made
            response.Result.Should().Be("Fred...");
        }

        static MessageExchangeProtocol GetProtocol(Stream stream, ILog logger)
        {
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            var activeConnectionLimiter = new ActiveTcpConnectionsLimiter(limits);
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder(new LogFactory()).Build(), new NoOpControlMessageObserver(), limits, logger), limits, activeConnectionLimiter, NoIdentityObserver.Instance, logger);
        }
    }
}