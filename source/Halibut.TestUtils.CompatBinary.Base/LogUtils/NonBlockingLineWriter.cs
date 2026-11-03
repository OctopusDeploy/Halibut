using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    /// <summary>
    /// Writes lines to a <see cref="TextWriter"/> from a dedicated background thread, so callers never block on the write.
    ///
    /// The compat binaries write their logs to stdout, which the test process reads through a pipe. If the test process
    /// stops reading, the pipe buffer (about 4KB on Windows) fills up and a plain Console.WriteLine blocks. Because Halibut
    /// logs on the request path, that blocked write freezes the whole compat binary: in-flight requests stop, and new
    /// connections are never accepted (see LEV-2253).
    ///
    /// With this writer, a stalled reader only stalls the background thread. Callers queue lines up to a fixed capacity,
    /// after which lines are dropped (and counted) rather than blocking the Halibut protocol.
    /// </summary>
    public class NonBlockingLineWriter : IDisposable
    {
        public const int DefaultCapacity = 10_000;

        readonly TextWriter target;
        readonly BlockingCollection<string> lines;
        readonly Thread writerThread;
        long droppedLines;
        long pendingLines;

        public NonBlockingLineWriter(TextWriter target, int capacity = DefaultCapacity, string threadName = nameof(NonBlockingLineWriter))
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");

            this.target = target ?? throw new ArgumentNullException(nameof(target));
            lines = new BlockingCollection<string>(new ConcurrentQueue<string>(), capacity);
            writerThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = threadName
            };
            writerThread.Start();
        }

        /// <summary>
        /// The number of lines dropped because the queue was full, and not yet reported to the target.
        /// </summary>
        public long DroppedLines => Interlocked.Read(ref droppedLines);

        /// <summary>
        /// Queues a line to be written. Never blocks. If the queue is full the line is dropped, and a count of dropped
        /// lines is written to the target once it starts accepting writes again.
        /// </summary>
        public void WriteLine(string line)
        {
            Interlocked.Increment(ref pendingLines);

            bool added;
            try
            {
                added = lines.TryAdd(line);
            }
            catch (InvalidOperationException)
            {
                // Disposed (CompleteAdding was called).
                added = false;
            }

            if (!added)
            {
                Interlocked.Decrement(ref pendingLines);
                Interlocked.Increment(ref droppedLines);
            }
        }

        /// <summary>
        /// Waits until every queued line has been written to the target, or the timeout elapses.
        /// Use this where the test process must see a line before continuing, such as "RunningAndReady".
        /// </summary>
        /// <returns>True if every queued line was written before the timeout.</returns>
        public async Task<bool> FlushAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (Interlocked.Read(ref pendingLines) > 0)
            {
                if (DateTime.UtcNow >= deadline) return false;
                await Task.Delay(10).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Synchronous version of <see cref="FlushAsync"/>, for use just before the process exits.
        /// </summary>
        public bool Flush(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (Interlocked.Read(ref pendingLines) > 0)
            {
                if (DateTime.UtcNow >= deadline) return false;
                Thread.Sleep(10);
            }

            return true;
        }

        void WriteLoop()
        {
            foreach (var line in lines.GetConsumingEnumerable())
            {
                try
                {
                    var dropped = Interlocked.Exchange(ref droppedLines, 0);
                    if (dropped > 0)
                    {
                        target.WriteLine($"[{nameof(NonBlockingLineWriter)}] {dropped} log line(s) were dropped because the output was not being read.");
                    }

                    target.WriteLine(line);
                    target.Flush();
                }
                catch (Exception)
                {
                    // Nothing sensible can be done if the target is broken (e.g. the pipe was closed),
                    // and an exception here must not take down the compat binary.
                }
                finally
                {
                    Interlocked.Decrement(ref pendingLines);
                }
            }
        }

        public void Dispose()
        {
            lines.CompleteAdding();
        }
    }
}
