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

            manager.AddActiveClient(thumbprint, new TcpClient());

            manager.GetActiveClients(thumbprint).Should().HaveCount(1);
        }

        [Test]
        public void AddShouldRemoveStaleClients()
        {
            const string thumbprint = "123";
            var manager = new TcpClientManager();

            manager.AddActiveClient(thumbprint, new TcpClient()); // this client is stale
            manager.AddActiveClient(thumbprint, new TcpClient());

            manager.GetActiveClients(thumbprint).Should().HaveCount(1);
        }

        [Test]
        public void ShouldDisconnect()
        {
            const string thumbprint = "123";
            var manager = new TcpClientManager();

            manager.AddActiveClient(thumbprint, new TcpClient());
            manager.Disconnect(thumbprint);

            manager.GetActiveClients(thumbprint).Should().BeEmpty();
        }
    }
}