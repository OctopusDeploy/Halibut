using System;
using System.Collections.Generic;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Tests.Support.Logging;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics.LogWriters
{
    public class AggregateLogWriterLogCreatorFixture
    {
        [Test]
        public void EachLogWriterShouldBeCalled()
        {
            var logWriter1 = new InMemoryLogWriter();
            var logWriter2 = new InMemoryLogWriter();

            var aggregateLogWriterLog = new AggregateLogWriterLogCreator(new InMemoryConnectionLogCreator(), prefix => { return new[] {logWriter1, logWriter2}; });

            var log = aggregateLogWriterLog.CreateNewForPrefix("poll://foo/");

            log.Write(EventType.Security, "Hello");

            logWriter1.GetLogs()[0].FormattedMessage.Should().Be("Hello");
            logWriter2.GetLogs()[0].FormattedMessage.Should().Be("Hello");
        }

        [Test]
        public void EachCallToCreateNewForPrefixShouldReturnANewLog()
        {
            var logWriter1 = new InMemoryLogWriter();
            var logWriter2 = new InMemoryLogWriter();

            var prefixesPassedIn = new List<string>();

            var aggregateLogWriterLog = new AggregateLogWriterLogCreator(new InMemoryConnectionLogCreator(), prefix =>
            {
                prefixesPassedIn.Add(prefix);

                return new[] {logWriter1, logWriter2};
            });

            var log1 = aggregateLogWriterLog.CreateNewForPrefix("poll://foo/");
            var log2 = aggregateLogWriterLog.CreateNewForPrefix("poll://foo/");

            ReferenceEquals(log1, log2).Should().BeFalse();

            prefixesPassedIn.Should().Contain("poll://foo/", "poll://foo/");
        }
    }
}