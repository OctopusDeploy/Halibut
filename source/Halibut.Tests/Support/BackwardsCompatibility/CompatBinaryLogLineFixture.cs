using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;
using NUnit.Framework;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class CompatBinaryLogLineFixture : BaseTest
    {
        static readonly DateTime WrittenAt = new(2026, 11, 1, 12, 28, 14, 740, DateTimeKind.Utc);

        [Test]
        public void WithTimestamp_PrefixesTheTimeTheLineWasWritten()
        {
            CompatBinaryLogLine.WithTimestamp(WrittenAt, "Listening on port: 53024")
                .Should().Be("[compat 12:28:14.740Z] Listening on port: 53024");
        }

        [TestCase("Listening on port: 53024")]
        [TestCase("RunningAndReady")]
        [TestCase("Polling listener is listening on port: 53127")]
        [TestCase("[compat ] not a timestamp")]
        [TestCase("")]
        public void WithoutTimestamp_RoundTrips(string line)
        {
            CompatBinaryLogLine.WithoutTimestamp(CompatBinaryLogLine.WithTimestamp(WrittenAt, line))
                .Should().Be(line);
        }

        [TestCase("Listening on port: 53024")]
        [TestCase("Compat binary is still running")]
        [TestCase("[compat without a terminator")]
        public void WithoutTimestamp_LeavesLinesWithoutAPrefixUnchanged(string line)
        {
            CompatBinaryLogLine.WithoutTimestamp(line).Should().Be(line);
        }

        [Test]
        public async Task NonBlockingLineWriter_TimestampsLinesWhenTheyAreWritten_NotWhenTheyReachTheTarget()
        {
            var now = WrittenAt;
            var target = new StringWriter();
            using var writer = new NonBlockingLineWriter(target, utcNow: () => now);

            writer.WriteLine("first");
            now = now.AddSeconds(1);
            writer.WriteLine("second");
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

            target.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Should().Equal("[compat 12:28:14.740Z] first", "[compat 12:28:15.740Z] second");
        }

        [Test]
        public async Task NonBlockingLineWriter_DoesNotTimestampLinesWithoutAClock()
        {
            var target = new StringWriter();
            using var writer = new NonBlockingLineWriter(target);

            writer.WriteLine("plain");
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

            target.ToString().Trim().Should().Be("plain");
        }
    }
}
