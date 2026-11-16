using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    /// <summary>
    /// LEV-2306 repro only. When HALIBUT_REPRO_COMPAT_TRACE_FILE is set, every console write is
    /// also appended to that file with the time the compat process wrote it. This lets the test
    /// compare "written by the compat process" against "received by the test" for each line.
    /// </summary>
    public class ReproTracingConsoleWriter : TextWriter
    {
        public const string TraceFileEnvVar = "HALIBUT_REPRO_COMPAT_TRACE_FILE";

        readonly TextWriter inner;
        readonly StreamWriter trace;
        readonly object traceLock = new();

        ReproTracingConsoleWriter(TextWriter inner, string traceFile)
        {
            this.inner = inner;
            var stream = new FileStream(traceFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            trace = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            Trace("PROCESS_STARTED pid=" + Process.GetCurrentProcess().Id);
        }

        public static void InstallIfRequested()
        {
            var traceFile = Environment.GetEnvironmentVariable(TraceFileEnvVar);
            if (string.IsNullOrEmpty(traceFile)) return;

            Console.SetOut(new ReproTracingConsoleWriter(Console.Out, traceFile!));
        }

        void Trace(string value)
        {
            lock (traceLock)
            {
                trace.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss.fffffff") + " " + value.Replace("\r", "\\r").Replace("\n", "\\n"));
            }
        }

        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            Trace("WRITE_CHAR " + value);
            inner.Write(value);
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            Trace("WRITE " + value);
            inner.Write(value);
        }

        public override void WriteLine(string? value)
        {
            Trace("WRITELINE " + value);
            inner.WriteLine(value);
        }

        public override void Flush() => inner.Flush();
    }
}
