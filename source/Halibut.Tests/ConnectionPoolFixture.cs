using System;
using FluentAssertions;
using Halibut.Transport;
using Xunit;

namespace Halibut.Tests
{
    public class ConnectionPoolFixture
    {
        ConnectionPool<string, Connection> pool;

        public ConnectionPoolFixture()
        {
            pool = new ConnectionPool<string, Connection>();
        }

        [Fact]
        public void ShouldGetConnectionFromPool()
        {
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

        [Fact]
        public void ShouldGetConnectionFromPoolByKey()
        {
            pool.Return("http://foo1", new Connection());
            pool.Return("http://foo2", new Connection());
            pool.Take("http://foo1").Should().NotBeNull();
            pool.Take("http://foo1").Should().BeNull();
        }

        [Fact]
        public void ShouldLetConnectionsExpire()
        {
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
