using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Threading;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    /// <summary>
    /// The test reads our stdout through a pipe. If it stops reading, a blocking Console.Write
    /// freezes Halibut mid-exchange, so writes are queued and dropped when the queue is full.
    /// </summary>
    public class NonBlockingConsoleWriter : TextWriter
    {
        const int MaxQueuedWrites = 10_000;

        readonly TextWriter destination;
        readonly BlockingCollection<string> queue = new(MaxQueuedWrites);
        long droppedWrites;

        NonBlockingConsoleWriter(Stream destination)
        {
            this.destination = new StreamWriter(destination, new UTF8Encoding(false)) { AutoFlush = false };
            new Thread(Drain) { IsBackground = true, Name = "NonBlockingConsoleWriter" }.Start();
        }

        public static void Install()
        {
            Console.SetOut(new NonBlockingConsoleWriter(OpenStandardStream(1, Console.OpenStandardOutput)));
            Console.SetError(new NonBlockingConsoleWriter(OpenStandardStream(2, Console.OpenStandardError)));
            Trace("installed " + Console.Out.GetType().FullName);
        }

        // On Unix the console stream locks Console.Out while writing, so a stalled pipe would
        // still block every Console.WriteLine. Write to the raw file descriptor instead.
        static Stream OpenStandardStream(int fileDescriptor, Func<Stream> openConsoleStream)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return openConsoleStream();
            return new FileStream(new SafeFileHandle((IntPtr)fileDescriptor, ownsHandle: false), FileAccess.Write, 1);
        }

        public override Encoding Encoding => destination.Encoding;

        public override void Write(char value) => Enqueue(value.ToString());

        public override void Write(string? value)
        {
            if (value is not null) Enqueue(value);
        }

        public override void WriteLine(string? value) => Enqueue(value + new string(CoreNewLine));

        static readonly string? TraceFile = Environment.GetEnvironmentVariable("HALIBUT_REPRO_COMPAT_TRACE_DIR") is { } dir
            ? Path.Combine(dir, $"compat-trace-{System.Diagnostics.Process.GetCurrentProcess().Id}.txt")
            : null;
        static readonly object TraceLock = new();

        static void Trace(string what)
        {
            if (TraceFile is null) return;
            lock (TraceLock) File.AppendAllText(TraceFile, $"{DateTime.Now:HH:mm:ss.fff} t{Thread.CurrentThread.ManagedThreadId} {what}\n");
        }

        void Enqueue(string value)
        {
            Trace("enqueue-start " + value.Substring(0, Math.Min(60, value.Length)).Replace("\n", " "));
            if (!queue.TryAdd(value)) Interlocked.Increment(ref droppedWrites);
            Trace("enqueue-done");
        }

        void Drain()
        {
            foreach (var value in queue.GetConsumingEnumerable())
            {
                var dropped = Interlocked.Exchange(ref droppedWrites, 0);
                if (dropped > 0) destination.WriteLine($"[NonBlockingConsoleWriter dropped {dropped} writes]");

                Trace("drain-write-start");
                destination.Write(value);
                if (queue.Count == 0) destination.Flush();
            }
        }
    }
}
