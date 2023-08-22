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

            var takeResult = await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken);
            takeResult.Should().BeNull();

            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);
            await pool.Return_SyncOrAsync(syncOrAsync, "http://foo", connection, CancellationToken);

            // Assert by taking twice. The second one should be null again
            takeResult = await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken);
            takeResult.Should().Be(connection);

            takeResult = await pool.Take_SyncOrAsync(syncOrAsync, "http://foo", CancellationToken);
            takeResult.Should().BeNull();
        }

        [Test]
        public async Task AsynchronousDisposalCanBeDoneOnSynchronousConnectionPool()
        {
            var connectionPool = SyncOrAsync.Sync.CreateConnectionPool<string, TestConnection>();

            await connectionPool.DisposeAsync();
        }

        [Test]
        public void SynchronousDisposalCanBeDoneOnAsynchronousConnectionPool()
        {
            var connectionPool = SyncOrAsync.Async.CreateConnectionPool<string, TestConnection>();

            connectionPool.Dispose();
        }
    }
}
