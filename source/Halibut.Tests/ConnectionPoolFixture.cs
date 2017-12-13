using System;
using FluentAssertions;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ConnectionPoolFixture
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
        public void ShouldGetConnectionFromPoolByKey()
        {
            var pool = new ConnectionPool<string, Connection>();
            pool.Return("http://foo1", new Connection());
            pool.Return("http://foo2", new Connection());
            pool.Take("http://foo1").Should().NotBeNull();
            pool.Take("http://foo1").Should().BeNull();
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
