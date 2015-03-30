using System;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class ConnectionPoolFixture
    {
        ConnectionPool<string, Connection> pool;

        [SetUp]
        public void SetUp()
        {
            pool = new ConnectionPool<string, Connection>();
        }

        [Test]
        public void ShouldGetConnectionFromPool()
        {
            pool.Return("http://foo", new Connection());
            pool.Return("http://foo", new Connection());
            pool.Return("http://foo", new Connection());
            Assert.That(pool.Take("http://foo"), Is.Not.Null);
            Assert.That(pool.Take("http://foo"), Is.Not.Null);
            Assert.That(pool.Take("http://foo"), Is.Not.Null);
            Assert.That(pool.Take("http://foo"), Is.Null);
            Assert.That(pool.Take("http://foo"), Is.Null);
            Assert.That(pool.Take("http://foo"), Is.Null);
            pool.Return("http://foo", new Connection());
            Assert.That(pool.Take("http://foo"), Is.Not.Null);
            Assert.That(pool.Take("http://foo"), Is.Null);
        }

        [Test]
        public void ShouldGetConnectionFromPoolByKey()
        {
            pool.Return("http://foo1", new Connection());
            pool.Return("http://foo2", new Connection());
            Assert.That(pool.Take("http://foo1"), Is.Not.Null);
            Assert.That(pool.Take("http://foo1"), Is.Null);
        }

        [Test]
        public void ShouldLetConnectionsExpire()
        {
            var connection = new Connection();

            pool.Return("http://foo", connection);
            Assert.That(connection.UsageCount, Is.EqualTo(1));

            Assert.That(pool.Take("http://foo"), Is.EqualTo(connection));
            pool.Return("http://foo", connection);
            Assert.That(connection.UsageCount, Is.EqualTo(2));
            
            Assert.That(pool.Take("http://foo"), Is.EqualTo(connection));
            pool.Return("http://foo", connection);
            Assert.That(connection.UsageCount, Is.EqualTo(3));

            Assert.That(pool.Take("http://foo"), Is.Null);
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
