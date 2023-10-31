using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class ConnectionPoolFixture : BaseTest
    {
        [Test]
        public async Task ShouldGetConnectionFromPool()
        {
            var pool = new ConnectionPoolAsync<string, TestConnection>();
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
        public async Task ShouldGetConnectionFromPoolByKey()
        {
            var pool = new ConnectionPoolAsync<string, TestConnection>();
            await pool.ReturnAsync("http://foo1", new TestConnection(), CancellationToken);
            await pool.ReturnAsync("http://foo2", new TestConnection(), CancellationToken);
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().NotBeNull();
            (await pool.TakeAsync("http://foo1", CancellationToken)).Should().BeNull();
        }
        
        [Test]
        public async Task ShouldLetConnectionsExpireAsync()
        {
            var pool = new ConnectionPoolAsync<string, TestConnection>();
            var connection = new TestConnection();

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(1);

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().Be(connection);
            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(2);

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().Be(connection);
            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            connection.UsageCount.Should().Be(3);
            connection.Expire();

            (await pool.TakeAsync("http://foo", CancellationToken)).Should().BeNull();
        }
        
        [Test]
        public async Task ShouldNotAllowMultipleReturnsOfSameConnectionAsync()
        {
            var pool = new ConnectionPoolAsync<string, TestConnection>();
            var connection = new TestConnection();

            var takeResult = await pool.TakeAsync("http://foo", CancellationToken);
            takeResult.Should().BeNull();

            await pool.ReturnAsync("http://foo", connection, CancellationToken);
            await pool.ReturnAsync("http://foo", connection, CancellationToken);

            // Assert by taking twice. The second one should be null again
            takeResult = await pool.TakeAsync("http://foo", CancellationToken);
            takeResult.Should().Be(connection);

            takeResult = await pool.TakeAsync("http://foo", CancellationToken);
            takeResult.Should().BeNull();
        }
    }
}
