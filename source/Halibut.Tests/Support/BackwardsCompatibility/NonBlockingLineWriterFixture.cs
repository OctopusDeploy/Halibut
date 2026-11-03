using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;
using NUnit.Framework;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class NonBlockingLineWriterFixture : BaseTest
    {
        [Test]
        public async Task WritesLinesInOrder()
        {
            var target = new StringWriter();
            using var writer = new NonBlockingLineWriter(target);

            for (var i = 0; i < 100; i++) writer.WriteLine($"line {i}");

            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

            ReadLines(target).Should().Equal(Enumerable.Range(0, 100).Select(i => $"line {i}"));
        }

        [Test]
        public async Task DoesNotBlockWhenTheTargetIsBlocked_AndReportsDroppedLinesOnceUnblocked()
        {
            const int capacity = 10;
            using var target = new BlockingTextWriter();
            using var writer = new NonBlockingLineWriter(target, capacity);

            // The first line is taken by the writer thread, which then blocks on the target.
            writer.WriteLine("first");
            target.WaitUntilAWriteIsBlocked(TimeSpan.FromSeconds(10)).Should().BeTrue();

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < capacity + 50; i++) writer.WriteLine($"queued {i}");
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "writing must never wait on a blocked target");
            writer.DroppedLines.Should().Be(50);

            target.Unblock();
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
            writer.WriteLine("after unblocking");
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

            var lines = ReadLines(target.Written);
            lines.Should().StartWith(new[] { "first" });
            lines.Should().ContainInOrder(Enumerable.Range(0, capacity).Select(i => $"queued {i}"));
            lines.Should().Contain(l => l.Contains("50 log line(s) were dropped"));
            lines.Last().Should().Be("after unblocking");
            writer.DroppedLines.Should().Be(0);
        }

        [Test]
        public async Task FlushReturnsFalseWhenTheTargetStaysBlocked()
        {
            using var target = new BlockingTextWriter();
            using var writer = new NonBlockingLineWriter(target);

            writer.WriteLine("stuck");
            target.WaitUntilAWriteIsBlocked(TimeSpan.FromSeconds(10)).Should().BeTrue();

            (await writer.FlushAsync(TimeSpan.FromMilliseconds(200))).Should().BeFalse();
            writer.Flush(TimeSpan.FromMilliseconds(200)).Should().BeFalse();

            target.Unblock();
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        }

        [Test]
        public async Task KeepsWritingWhenTheTargetThrows()
        {
            var target = new ThrowOnceTextWriter();
            using var writer = new NonBlockingLineWriter(target);

            writer.WriteLine("throws");
            writer.WriteLine("written");
            (await writer.FlushAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

            ReadLines(target.Written).Should().Equal("written");
        }

        static string[] ReadLines(StringWriter writer) =>
            writer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Simulates a stdout pipe that nobody is reading: writes block until <see cref="Unblock"/> is called.
        /// </summary>
        class BlockingTextWriter : TextWriter
        {
            readonly ManualResetEventSlim unblocked = new(false);
            readonly ManualResetEventSlim aWriteIsBlocked = new(false);

            public StringWriter Written { get; } = new();
            public override Encoding Encoding => Encoding.UTF8;

            public bool WaitUntilAWriteIsBlocked(TimeSpan timeout) => aWriteIsBlocked.Wait(timeout);
            public void Unblock() => unblocked.Set();

            public override void WriteLine(string? value)
            {
                if (!unblocked.IsSet)
                {
                    aWriteIsBlocked.Set();
                    unblocked.Wait();
                }

                lock (Written) Written.WriteLine(value);
            }

            protected override void Dispose(bool disposing)
            {
                // Never leave the writer thread blocked after a test.
                unblocked.Set();
                base.Dispose(disposing);
            }
        }

        class ThrowOnceTextWriter : TextWriter
        {
            bool hasThrown;

            public StringWriter Written { get; } = new();
            public override Encoding Encoding => Encoding.UTF8;

            public override void WriteLine(string? value)
            {
                if (!hasThrown)
                {
                    hasThrown = true;
                    throw new IOException("The pipe has been ended.");
                }

                Written.WriteLine(value);
            }
        }
    }
}
