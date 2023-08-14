using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    [TestFixture]
    public class ConnectionManagerFixture
    {
        public (IConnectionFactory ConnectionFactory, ExchangeProtocolBuilder ExchngeProtocolBuilder) SetUp(SyncOrAsync syncOrAsync)
        {
            MessageExchangeProtocol ExchangeProtocolBuilder(Stream s, ILog l) => GetProtocol(s, l, syncOrAsync);

            var connection = new SecureConnection(Substitute.For<IDisposable>(), Stream.Null, ExchangeProtocolBuilder, Substitute.For<ILog>());
            var connectionFactory = Substitute.For<IConnectionFactory>();
            connectionFactory.EstablishNewConnection(ExchangeProtocolBuilder, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>()).Returns(connection);

            return (connectionFactory, ExchangeProtocolBuilder);
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager(new HalibutTimeoutsAndLimits());

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);

            connectionManager.Disconnect(serviceEndpoint, null);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task ReleasedConnectionsAreNotActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager(new HalibutTimeoutsAndLimits());

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager(new HalibutTimeoutsAndLimits());

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            activeConnection.Dispose();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisconnectDisposesActiveConnections(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager(new HalibutTimeoutsAndLimits());

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog log, SyncOrAsync syncOrAsync)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder(new LogFactory()).Build(), syncOrAsync.ToAsyncHalibutFeature(), new HalibutTimeoutsAndLimits(), log), log);
        }
    }
}