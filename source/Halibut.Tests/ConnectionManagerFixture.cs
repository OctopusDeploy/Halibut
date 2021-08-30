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
            connection = new SecureConnection(Substitute.For<IDisposable>(), Stream.Null,GetProtocol, Substitute.For<ILog>());
            connectionFactory = Substitute.For<IConnectionFactory>();
            connectionFactory.EstablishNewConnection(GetProtocol, Arg.Any<ServiceEndPoint>(), Arg.Any<ILog>()).Returns(connection);
        }

        [Test]
        public void DisposedConnectionsAreRemovedFromActive_WhenMultipleConnectionsAreActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            //do it twice because this bug only triggers on multiple enumeration, having 1 in the collection doesn't trigger the bug
            connectionManager.AcquireConnection(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.AcquireConnection(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));

            connectionManager.Disconnect(serviceEndpoint, null);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }
       
        [Test]
        public void ReleasedConnectionsAreNotActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            var activeConnection = connectionManager.AcquireConnection(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.ReleaseConnection(serviceEndpoint, activeConnection);
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisposedConnectionsAreRemovedFromActive()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();

            var activeConnection = connectionManager.AcquireConnection(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            activeConnection.Dispose();
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        [Test]
        public void DisconnectDisposesActiveConnections()
        {
            var serviceEndpoint = new ServiceEndPoint("https://localhost:42", Certificates.TentacleListeningPublicThumbprint);
            var connectionManager = new ConnectionManager();
            
            var activeConnection = connectionManager.AcquireConnection(GetProtocol, connectionFactory, serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().OnlyContain(c => c == activeConnection);

            connectionManager.Disconnect(serviceEndpoint, new InMemoryConnectionLog(serviceEndpoint.ToString()));
            connectionManager.GetActiveConnections(serviceEndpoint).Should().BeNullOrEmpty();
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog log)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializer(), log), log);
        }
    }
}