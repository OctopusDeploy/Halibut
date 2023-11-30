using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
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
        public async Task DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive()
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections();

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            await connectionManager.DisconnectAsync(serviceEndpoint, null!, CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public async Task ReleasedConnectionsAreNotActive()
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections();

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            await connectionManager.ReleaseConnectionAsync(serviceEndpoint, activeConnection, CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public async Task DisposedConnectionsAreRemovedFromActive()
        {
            var connectionFactory = CreateFactoryThatCreatesTestConnections();

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            await activeConnection.DisposeAsync();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public async Task DisconnectDisposesActiveConnections()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            await connectionManager.DisconnectAsync(serviceEndpoint, null!, CancellationToken);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }
        
        [Test]
        public async Task DisconnectDisposesReleasedConnections()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            await connectionManager.ReleaseConnectionAsync(serviceEndpoint, activeConnection, CancellationToken);

            await connectionManager.DisconnectAsync(serviceEndpoint, null!, CancellationToken);

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task DisposingConnectionManagerDisposesActiveConnections()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using (var connectionManager = new ConnectionManagerAsync())
            {
                var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);
            }

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task DisposingConnectionManagerDisposesConnectionsInConnectionPool()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using (var connectionManager = new ConnectionManagerAsync())
            {
                var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                await connectionManager.ReleaseConnectionAsync(serviceEndpoint, activeConnection, CancellationToken);
            }

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task AcquireConnection_WhenConnectionInPoolHasExpired_CreatesNewConnectionAndDisposesExpiredConnection()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);
            
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
            await connectionManager.ReleaseConnectionAsync(serviceEndpoint, activeConnection, CancellationToken);

            var testConnection = createdTestConnections.Should().ContainSingle().Subject;
            testConnection.Expire();

            var newActiveConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);

            createdTestConnections.Should().HaveCount(2);
            newActiveConnection.Should().NotBe(activeConnection);

            testConnection.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task ReleaseConnection_WillKeep5ConnectionsInPool_AndDisposeOldConnections()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var connections = new List<IConnection>();
            for (int i = 0; i < 10; i++)
            {
                var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken);
                connections.Add(activeConnection);
            }

            foreach (var connection in connections)
            {
                await connectionManager.ReleaseConnectionAsync(serviceEndpoint, connection, CancellationToken);
            }

            createdTestConnections.Where(c => c.Disposed).Should().HaveCount(5);
            createdTestConnections.Where(c => !c.Disposed).Should().HaveCount(5);
        }

        [Test]
        public async Task ClearPooledConnectionsDisposesAllConnectionsInPool()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            var activeConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);

            await connectionManager.ReleaseConnectionAsync(serviceEndpoint, activeConnection, CancellationToken);

            await connectionManager.ClearPooledConnectionsAsync(serviceEndpoint, inMemoryConnectionLog, CancellationToken);

            var createdTestConnection = createdTestConnections.Should().ContainSingle().Subject;
            createdTestConnection.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task DisconnectClearsPoolAndActiveConnection()
        {
            var createdTestConnections = new List<TestConnection>();
            var connectionFactory = CreateFactoryThatCreatesTestConnections(createdTestConnections.Add);

            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint, new HalibutTimeoutsAndLimitsForTestsBuilder().Build());
            await using var connectionManager = new ConnectionManagerAsync();

            var inMemoryConnectionLog = new InMemoryConnectionLog(serviceEndpoint.ToString());
            await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            var returnedConnection = await connectionManager.AcquireConnectionAsync(GetProtocol, connectionFactory, serviceEndpoint, inMemoryConnectionLog, CancellationToken);
            await connectionManager.ReleaseConnectionAsync(serviceEndpoint, returnedConnection, CancellationToken);
            
            await connectionManager.DisconnectAsync(serviceEndpoint, null!, CancellationToken);

            createdTestConnections.Should().HaveCount(2);
            createdTestConnections.Should().AllSatisfy(c => c.Disposed.Should().BeTrue());
        }
        
        static IConnectionFactory CreateFactoryThatCreatesTestConnections(Action<TestConnection>? connectionCreated = null)
        {
            var connectionFactory = Substitute.For<IConnectionFactory>();
            
            connectionFactory.EstablishNewConnectionAsync(GetProtocol, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    var testConnection = new TestConnection();
                    connectionCreated?.Invoke(testConnection);
                    return testConnection;
                });

            return connectionFactory;
        }

        static MessageExchangeProtocol GetProtocol(Stream stream, ILog log)
        {
            throw new NotImplementedException("Not important for this test");
        }
    }
}