using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class ConnectionManagerFixture : BaseTest
    {
        public (IConnectionFactory ConnectionFactory, ExchangeProtocolBuilder ExchngeProtocolBuilder) SetUp(SyncOrAsync syncOrAsync
            //TODO: Value in real connection?
            , bool testConnection = false)
        {
            //TODO: Verify this is actually used/needed
            MessageExchangeProtocol ExchangeProtocolBuilder(Stream s, ILog l) => GetProtocol(s, l, syncOrAsync);

            // TODO: Use EstablishNewConnectionAsync. But did the tests just work without it??
            if (testConnection)
            {
                var connectionFactory = Substitute.For<IConnectionFactory>();
                connectionFactory.EstablishNewConnection(ExchangeProtocolBuilder, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                    .Returns(_ => new TestConnection());

                return (connectionFactory, ExchangeProtocolBuilder);
            }
            else
            {
                var connection = new SecureConnection(Substitute.For<IDisposable>(), Stream.Null, ExchangeProtocolBuilder, Substitute.For<ILog>());
                var connectionFactory = Substitute.For<IConnectionFactory>();
                connectionFactory.EstablishNewConnection(ExchangeProtocolBuilder, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>()).Returns(connection);

                return (connectionFactory, ExchangeProtocolBuilder);
            }
            
        }

        public (IConnectionFactory ConnectionFactory, ExchangeProtocolBuilder ExchngeProtocolBuilder, List<TestConnection> connectionsCreated) SetUpWithTestConnection(SyncOrAsync syncOrAsync)
        {
            //TODO: Verify this is actually used/needed
            MessageExchangeProtocol ExchangeProtocolBuilder(Stream s, ILog l) => GetProtocol(s, l, syncOrAsync);

            var connectionFactory = Substitute.For<IConnectionFactory>();
            var connectionsCreated = new List<TestConnection>();
            connectionFactory.EstablishNewConnection(ExchangeProtocolBuilder, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    connectionsCreated.Add(new TestConnection());
                    return connectionsCreated.Last();
                });
            connectionFactory.EstablishNewConnectionAsync(ExchangeProtocolBuilder, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    connectionsCreated.Add(new TestConnection());
                    return connectionsCreated.Last();
                });

            return (connectionFactory, ExchangeProtocolBuilder, connectionsCreated);
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            connectionManager.Disconnect(serviceEndpoint, null);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task ReleasedConnectionsAreNotActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            await connectionManager.ReleaseConnection_SyncOrAsync(syncOrAsync, serviceEndpoint, activeConnection, CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
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
            using var connectionManager = new ConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }


        [Test]
        [SyncAndAsync]
        public async Task DisconnectDisposesReleasedConnections(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            //TODO VERIFY
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposingConnectionManagerDisposesActiveConnections(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using (var connectionManager = new ConnectionManager())
            {

                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);
            }

            //TODO Verify
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposingConnectionManagerDisposesConnectionsInConnectionPool(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder) = SetUp(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using (var connectionManager = new ConnectionManager())
            {

                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            }

            //TODO Verify
        }

        [Test]
        [SyncAndAsync]
        public async Task AcquireConnectionCreatesNewConnectionIfConnectionInPoolHasExpired(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder, connectionsCreated) = SetUpWithTestConnection(syncOrAsync);

            
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);

            var testConnection = connectionsCreated.Should().ContainSingle().Subject;
            testConnection.Expire();

            var newActiveConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            connectionsCreated.Should().HaveCount(2);
            newActiveConnection.Should().NotBe(activeConnection);

            testConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task ReleaseConnectionCreatesNewConnectionIfConnectionInPoolHasExpired(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder, connectionsCreated) = SetUpWithTestConnection(syncOrAsync);
            
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var connections = new List<IConnection>();
            for (int i = 0; i < 10; i++)
            {
                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connections.Add(activeConnection);

                connectionsCreated[i].Expire();
            }

            foreach (var connection in connections)
            {
                connectionManager.ReleaseConnection(serviceEndpoint, connection);
            }
            
            //TODO: Verify
        }

        [Test]
        [SyncAndAsync]
        public async Task ClearPooledConnectionsDisposesAllConnectionsInPool(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder, connectionsCreated) = SetUpWithTestConnection(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);


            connectionManager.ClearPooledConnections(serviceEndpoint, inMemoryConnectionLog);

            //TODO: Verify
        }

        [Test]
        [SyncAndAsync]
        public async Task DisconnectClearsPoolAndActiveConnection(SyncOrAsync syncOrAsync)
        {
            var (connectionFactory, exchangeProtocolBuilder, connectionsCreated) = SetUpWithTestConnection(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = new ConnectionManager();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            var returnedConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, exchangeProtocolBuilder, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            connectionManager.ReleaseConnection(serviceEndpoint, returnedConnection);


            connectionManager.Disconnect(serviceEndpoint, inMemoryConnectionLog);

            //TODO: Verify
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog log, SyncOrAsync syncOrAsync)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder().Build(), syncOrAsync.ToAsyncHalibutFeature(), log), log);
        }
    }
}