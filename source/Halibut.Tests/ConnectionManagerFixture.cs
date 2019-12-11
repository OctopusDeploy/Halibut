using System;
using System.IO;
using System.Threading;
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
            connectionFactory.EstablishNewConnection(Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>(), CancellationToken.None).Returns(connection);
        }

        [Test]
        public void DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);

            connectionManager.Disconnect(serviceEndpoint, null);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }
       
        [Test]
        public void ReleasedConnectionsAreNotActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            var activeConnection = connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisposedConnectionsAreRemovedFromActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            var activeConnection = connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            activeConnection.Dispose();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisconnectDisposesActiveConnections()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();
            
            var activeConnection = connectionManager.AcquireConnection(connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()), CancellationToken.None);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }
    }
}