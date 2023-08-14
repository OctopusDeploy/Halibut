using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class ConnectionPoolFixture : BaseTest
    {
        [Test]
        public void ShouldGetConnectionFromPool()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            pool.Return("http://foo", new TestConnection());
            pool.Return("http://foo", new TestConnection());
            pool.Return("http://foo", new TestConnection());
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Return("http://foo", new TestConnection());
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().BeNull();
        }

        [Test]
        public async Task ShouldGetConnectionFromPoolAsync()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            await pool.ReturnAsync("http://foo", new TestConnection(), CancellationToken);
            await pool.ReturnAsync("http://foo", new TestConnection(), CancellationToken);
            await pool.ReturnAsync("http://foo", new TestConnection(), CancellationToken);
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            await pool.ReturnAsync("http://foo", new TestConnection(), CancellationToken);
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
        }

        [Test]
        public void ShouldGetConnectionFromPoolByKey()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            pool.Return("http://foo1", new TestConnection());
            pool.Return("http://foo2", new TestConnection());
            pool.Take("http://foo1").Should().NotBeNull();
            pool.Take("http://foo1").Should().BeNull();
        }

        [Test]
        public async Task ShouldGetConnectionFromPoolByKeyAsync()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            await pool.ReturnAsync("http://foo1", new TestConnection(), CancellationToken);
            await pool.ReturnAsync("http://foo2", new TestConnection(), CancellationToken);
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().BeNull();
        }

        [Test]
        public void ShouldLetConnectionsExpire()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            pool.Return("http://foo", connection);
            connection.UsageCount.Should().Be(1);

            pool.Take("http://foo").Should().Be(connection);
            pool.Return("http://foo", connection);
            connection.UsageCount.Should().Be(2);

            pool.Take("http://foo").Should().Be(connection);
            pool.Return("http://foo", connection);
            connection.UsageCount.Should().Be(3);

            pool.Take("http://foo").Should().BeNull();
        }

        [Test]
        public async Task ShouldLetConnectionsExpireAsync()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(1);

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().Be(connection);
            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(2);

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().Be(connection);
            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(3);

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
        }

        [Test]
        public void ShouldNotAllowMultipleReturnsOfSameConnection()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            pool.GetTotalConnectionCount().Should().Be(0);

            pool.Return("http://foo", connection);
            pool.GetTotalConnectionCount().Should().Be(1);

            pool.Return("http://foo", connection);
            pool.GetTotalConnectionCount().Should().Be(1);
        }

        [Test]
        public async Task ShouldNotAllowMultipleReturnsOfSameConnectionAsync()
        {
            var pool = new ConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            pool.GetTotalConnectionCount().Should().Be(0);

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);
        }
    }

    //TODO: Move to new home
    public class TestConnection : IConnection
    {
        bool hasExpired;
        public void NotifyUsed()
        {
            UsageCount++;
            if (UsageCount >= 3)
            {
                hasExpired = true;
            }
        }

        public bool HasExpired()
        {
            return hasExpired;
        }

        public int UsageCount { get; private set; }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }

        public MessageExchangeProtocol Protocol { get; }

        public void Expire()
        {
            hasExpired = true;
        }
    }
}
