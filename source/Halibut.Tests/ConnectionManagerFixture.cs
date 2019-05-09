using System;
using System.IO;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class ConnectionManagerFixture
    {
        IConnection connection;
        IConnectionFactory connectionFactory;

        [SetUp]
        public void SetUp()
        {
            var stream = Substitute.For<IMessageExchangeStream>();
            connection = new SecureConnection(Substitute.For<IDisposable>(), Stream.Null, new MessageExchangeProtocol(stream));
            connectionFactory = Substitute.For<IConnectionFactory>();
            connectionFactory.EstablishNewConnection(Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>()).Returns(connection);
        }

        [Test]
        public void ReleasedConnectionsAreNotActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == connection);

            connectionManager.ReleaseConnection(serviceEndpoint, connection);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisposedConnectionsAreRemovedFromActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == connection);

            connection.Dispose();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisconnectDisposesActiveConnections()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();
            
            connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == connection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }
    }
}