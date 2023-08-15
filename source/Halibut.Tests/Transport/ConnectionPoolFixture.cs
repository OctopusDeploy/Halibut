using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class ConnectionPoolFixture : BaseTest
    {
        [Test]
        [SyncAndAsync]
        public async Task ShouldGetConnectionFromPool(SyncOrAsync syncOrAsync)
        {
            var pool = syncOrAsync.CreateConnectionPool<string, TestConnection>();
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", new TestConnection(), CancellationToken);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", new TestConnection(), CancellationToken);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", new TestConnection(), CancellationToken);
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().BeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().BeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().BeNull();
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", new TestConnection(), CancellationToken);
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().NotBeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().BeNull();
        }

        [Test]
        [SyncAndAsync]
        public async Task ShouldGetConnectionFromPoolByKey(SyncOrAsync syncOrAsync)
        {
            var pool = syncOrAsync.CreateConnectionPool<string, TestConnection>();
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo1", new TestConnection(), CancellationToken);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo2", new TestConnection(), CancellationToken);
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo1", CancellationToken)).Should().NotBeNull();
            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo1", CancellationToken)).Should().BeNull();
        }
        
        [Test]
        [SyncAndAsync]
        public async Task ShouldLetConnectionsExpireAsync(SyncOrAsync syncOrAsync)
        {
            var pool = syncOrAsync.CreateConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(1);

            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().Be(connection);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(2);

            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().Be(connection);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(3);
            connection.Expire();

            (await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken)).Should().BeNull();
        }
        
        [Test]
        [SyncAndAsync]
        public async Task ShouldNotAllowMultipleReturnsOfSameConnectionAsync(SyncOrAsync syncOrAsync)
        {
            var pool = syncOrAsync.CreateConnectionPool<string, TestConnection>();
            var connection = new TestConnection();

            pool.GetTotalConnectionCount().Should().Be(0);

            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);

            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            pool.GetTotalConnectionCount().Should().Be(1);
        }
    }
}
