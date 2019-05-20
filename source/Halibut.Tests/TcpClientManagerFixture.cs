using System.Net.Sockets;
using FluentAssertions;
using Halibut.Transport;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TcpClientManagerFixture
    {
        [Test]
        public void ShouldAddActiveClients()
        {
            const string thumbprint = "123";
            var manager = new TcpClientManager();
            manager.AddActiveClient(thumbprint, ConnectedTcpClient());
            manager.AddActiveClient(thumbprint, ConnectedTcpClient());

            manager.GetActiveClients(thumbprint).Should().HaveCount(2);
        }

        TcpClient ConnectedTcpClient()
        {
            var client = Substitute.ForPartsOf<TcpClient>();
            client.Connected.Returns(true);
            return client;
        }
    }
}