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
        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive(SyncOrAsync syncOrAsync)
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            connectionManager.Disconnect(serviceEndpoint, null);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task ReleasedConnectionsAreNotActive(SyncOrAsync syncOrAsync)
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            await connectionManager.ReleaseConnection_SyncOrAsync(syncOrAsync, serviceEndpoint, activeConnection, CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposedConnectionsAreRemovedFromActive(SyncOrAsync syncOrAsync)
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            activeConnection.Dispose();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisconnectDisposesActiveConnections(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }
        
        [Test]
        [SyncAndAsync]
        public async Task DisconnectDisposesReleasedConnections(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposingConnectionManagerDisposesActiveConnections(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using (var connectionManager = syncOrAsync.CreateConnectionManager())
            {
                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);
            }

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisposingConnectionManagerDisposesConnectionsInConnectionPool(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using (var connectionManager = syncOrAsync.CreateConnectionManager())
            {
                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            }

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task AcquireConnection_WhenConnectionInPoolHasExpired_CreatesNewConnectionAndDisposesExpiredConnection(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);
            
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);

            var testConnection = createdTestConnections.Should().ContainSingle().Subject;
            testConnection.Expire();

            var newActiveConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            createdTestConnections.Should().HaveCount(2);
            newActiveConnection.Should().NotBe(activeConnection);

            testConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task ReleaseConnection_WillKeep5ConnectionsInPool_AndDisposeOldConnections(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var connections = new List<IConnection>();
            for (int i = 0; i < 10; i++)
            {
                var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connections.Add(activeConnection);
            }

            foreach (var connection in connections)
            {
                connectionManager.ReleaseConnection(serviceEndpoint, connection);
            }

            createdTestConnections.Where(c => c.Disposed).Should().HaveCount(5);
            createdTestConnections.Where(c => !c.Disposed).Should().HaveCount(5);
        }

        [Test]
        [SyncAndAsync]
        public async Task ClearPooledConnectionsDisposesAllConnectionsInPool(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            var activeConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            
            connectionManager.ClearPooledConnections(serviceEndpoint, inMemoryConnectionLog);

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        [SyncAndAsync]
        public async Task DisconnectClearsPoolAndActiveConnection(SyncOrAsync syncOrAsync)
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(syncOrAsync, createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            using var connectionManager = syncOrAsync.CreateConnectionManager();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            var returnedConnection = await connectionManager.AcquireConnection_SyncOrAsync(syncOrAsync, GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            connectionManager.ReleaseConnection(serviceEndpoint, returnedConnection);
            
            connectionManager.Disconnect(serviceEndpoint, inMemoryConnectionLog);

            createdTestConnections.Should().HaveCount(2);
            createdTestConnections.Should().AllSatisfy(c => c.Disposed.Should().BeTrue());
        }

        static IConnectionFactory CreateFactoryThatCreatesTestConnections(SyncOrAsync syncOrAsync, Action<TestConnection>? connectionCreated = null)
        {
            var connectionFactory = Substitute.For<IConnectionFactory>();
            syncOrAsync
                .WhenSync(() =>
                    {
                        connectionFactory.EstablishNewConnection(GetProtocol, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                            .Returns(_ =>
                            {
                                var testConnection = new TestConnection();
                                connectionCreated?.Invoke(testConnection);
                                return testConnection;
                            });
                    }
                ).WhenAsync(() =>
                {
                    connectionFactory.EstablishNewConnectionAsync(GetProtocol, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                        .Returns(_ =>
                        {
                            var testConnection = new TestConnection();
                            connectionCreated?.Invoke(testConnection);
                            return testConnection;
                        });
                });

            return connectionFactory;
        }

        static MessageExchangeProtocol GetProtocol(Stream stream, ILog log)
        {
            //return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder().Build(), log), log);
            throw new NotImplementedException("Not important for this test");
        }
    }
}