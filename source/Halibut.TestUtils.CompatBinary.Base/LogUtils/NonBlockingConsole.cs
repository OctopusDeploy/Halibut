using System;
using System.Threading.Tasks;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    /// <summary>
    /// Use this instead of <see cref="Console.WriteLine(string)"/> everywhere in the compat binaries.
    /// See <see cref="NonBlockingLineWriter"/> for why.
    /// </summary>
    public static class NonBlockingConsole
    {
        static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromSeconds(10);

        static readonly NonBlockingLineWriter Writer = new(Console.Out, threadName: "CompatBinaryConsoleWriter", utcNow: () => DateTime.UtcNow);

        public static void WriteLine(string line) => Writer.WriteLine(line);

        /// <summary>
        /// Waits for queued lines to reach stdout, e.g. so the test process sees "RunningAndReady".
        /// </summary>
        public static Task<bool> FlushAsync() => Writer.FlushAsync(DefaultFlushTimeout);

        /// <summary>
        /// Waits (briefly) for queued lines to reach stdout before the process exits.
        /// </summary>
        public static bool FlushBeforeExit() => Writer.Flush(TimeSpan.FromSeconds(2));
    }
}
