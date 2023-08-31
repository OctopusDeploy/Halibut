using System;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Tests.Support.Logging;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics
{
    public class CachingLogFactoryFixture
    {
        [Test]
        public void TheSameILogIsReturnedForTheSamePrefix()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            var first = cachingLogFactory.ForPrefix("poll://foo/");
            var second = cachingLogFactory.ForPrefix("poll://foo/");
            ReferenceEquals(first, second).Should().BeTrue();
        }

        [Test]
        public void TheSameILogIsReturnedForTheSameEndPoint()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            var first = cachingLogFactory.ForEndpoint(new Uri("poll://bar/"));
            var second = cachingLogFactory.ForEndpoint(new Uri("poll://bar/"));
            ReferenceEquals(first, second).Should().BeTrue();
        }

        [Test]
        public void TheSameILogIsReturnedForTheSameEndPointAndPrefix()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            var first = cachingLogFactory.ForPrefix("poll://bar/");
            var second = cachingLogFactory.ForEndpoint(new Uri("poll://bar/"));
            ReferenceEquals(first, second).Should().BeTrue();
        }

        [Test]
        public void ADifferentILogIsReturnedForTheDifferentPrefixes()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            var first = cachingLogFactory.ForPrefix("poll://foo1/");
            var second = cachingLogFactory.ForPrefix("poll://foo2/");
            ReferenceEquals(first, second).Should().BeFalse();
        }

        [Test]
        public void ADifferentILogIsReturnedForTheDifferentEndPoints()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            var first = cachingLogFactory.ForEndpoint(new Uri("poll://bar1/"));
            var second = cachingLogFactory.ForEndpoint(new Uri("poll://bar2/"));
            ReferenceEquals(first, second).Should().BeFalse();
        }

        [Test]
        public void CachingAInMemoryConnectionLogMeansTheLogsAreRetained()
        {
            var cachingLogFactory = new InMemoryConnectionLogCreator().ToCachingLogFactory();

            cachingLogFactory.ForPrefix("poll://foo1/")
                .Write(EventType.Diagnostic, "Hello from prefix");
            cachingLogFactory.ForEndpoint(new Uri("poll://foo1/"))
                .Write(EventType.Diagnostic, "Hello from endpoint");
            cachingLogFactory.ForPrefix("poll://foo1/")
                .Write(EventType.Diagnostic, "cya");

            var logs = cachingLogFactory.ForPrefix("poll://foo1/")
                .GetLogs();

            logs[0].Message.Should().Be("Hello from prefix");
            logs[1].Message.Should().Be("Hello from endpoint");
            logs[2].Message.Should().Be("cya");
        }

        /// <summary>
        ///     Something similar to what we actually want to do, so lets test it here.
        /// </summary>
        [Test]
        public void CachingWithAggregateLogWriterLogCreatorAndInMemoryConnectionLogCreatorWorksAsExpected()
        {
            var logWriter = new InMemoryLogWriter();
            int callCount = 0;

            var cachingLogFactory = new AggregateLogWriterLogCreator(new InMemoryConnectionLogCreator(), prefix =>
            {
                callCount++;
                return new[] {logWriter};
            }).ToCachingLogFactory();

            cachingLogFactory.ForPrefix("poll://foo1/")
                .Write(EventType.Diagnostic, "Hello from prefix");
            cachingLogFactory.ForEndpoint(new Uri("poll://foo1/"))
                .Write(EventType.Diagnostic, "Hello from endpoint");

            var logs = cachingLogFactory.ForPrefix("poll://foo1/")
                .GetLogs();

            logs[0].Message.Should().Be("Hello from prefix");
            logs[1].Message.Should().Be("Hello from endpoint");

            callCount.Should().Be(1, "Since we should be caching the log writer");

            var logsFromWriter = logWriter.GetLogs();
            logsFromWriter[0].Message.Should().Be("Hello from prefix");
            logsFromWriter[1].Message.Should().Be("Hello from endpoint");
        }
    }
}