using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class ConnectionPoolFixture : BaseTest
    {
        [Test]
        public void ShouldGetConnectionFromPool()
        {
            var pool = new ConnectionPool<string, Connection>();
            pool.Return("http://foo", new Connection());
            pool.Return("http://foo", new Connection());
            pool.Return("http://foo", new Connection());
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Take("http://foo").Should().BeNull();
            pool.Return("http://foo", new Connection());
            pool.Take("http://foo").Should().NotBeNull();
            pool.Take("http://foo").Should().BeNull();
        }

        [Test]
        public async Task ShouldGetConnectionFromPoolAsync()
        {
            var pool = new ConnectionPool<string, Connection>();
            await pool.ReturnAsync("http://foo", new Connection(), CancellationToken);
            await pool.ReturnAsync("http://foo", new Connection(), CancellationToken);
            await pool.ReturnAsync("http://foo", new Connection(), CancellationToken);
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
            await pool.ReturnAsync("http://foo", new Connection(), CancellationToken);
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
        }

        [Test]
        public void ShouldGetConnectionFromPoolByKey()
        {
            var pool = new ConnectionPool<string, Connection>();
            pool.Return("http://foo1", new Connection());
            pool.Return("http://foo2", new Connection());
            pool.Take("http://foo1").Should().NotBeNull();
            pool.Take("http://foo1").Should().BeNull();
        }

        [Test]
        public async Task ShouldGetConnectionFromPoolByKeyAsync()
        {
            var pool = new ConnectionPool<string, Connection>();
            await pool.ReturnAsync("http://foo1", new Connection(), CancellationToken);
            await pool.ReturnAsync("http://foo2", new Connection(), CancellationToken);
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().BeNull();
        }

        [Test]
        public void ShouldLetConnectionsExpire()
        {
            var pool = new ConnectionPool<string, Connection>();
            var connection = new Connection();

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
            var pool = new ConnectionPool<string, Connection>();
            var connection = new Connection();

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
            var pool = new ConnectionPool<string, Connection>();
            var connection = new Connection();

            pool.GetTotalConnectionCount().Should().Be(0);

            pool.Return("http://foo", connection);
            pool.GetTotalConnectionCount().Should().Be(1);

            pool.Return("http://foo", connection);
            pool.GetTotalConnectionCount().Should().Be(1);
        }

        [Test]
        public async Task ShouldNotAllowMultipleReturnsOfSameConnectionAsync()
        {
            var pool = new ConnectionPool<string, Connection>();
            var connection = new Connection();

            pool.GetTotalConnectionCount().Should().Be(0);

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);
        }

        class Connection : IPooledResource
        {
            public void NotifyUsed()
            {
                UsageCount++;
            }

            public bool HasExpired()
            {
                return UsageCount >= 3;
            }

            public int UsageCount { get; private set; }

            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
