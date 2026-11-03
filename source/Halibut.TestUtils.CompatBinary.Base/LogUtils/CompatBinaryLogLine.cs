using System;
using System.Globalization;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    /// <summary>
    /// The compat binaries prefix each stdout line with the time it was written, e.g.
    /// <c>[compat 12:28:14.740Z] Listening on port: 53024</c>.
    ///
    /// The test process timestamps lines when it reads them, so without this a trace log cannot tell
    /// "the compat binary stopped writing" apart from "the test process stopped reading" (see LEV-2253).
    ///
    /// This file is linked into Halibut.Tests, so the test harness can strip the prefix before parsing
    /// lines such as "Listening on port: ".
    /// </summary>
    public static class CompatBinaryLogLine
    {
        const string Prefix = "[compat ";
        const string Terminator = "Z] ";

        public static string WithTimestamp(DateTime utcNow, string line)
        {
            return Prefix + utcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + Terminator + line;
        }

        /// <summary>
        /// Returns the line without its timestamp prefix. Lines without a prefix are returned unchanged.
        /// </summary>
        public static string WithoutTimestamp(string line)
        {
            if (!line.StartsWith(Prefix, StringComparison.Ordinal)) return line;

            var terminatorIndex = line.IndexOf(Terminator, Prefix.Length, StringComparison.Ordinal);
            if (terminatorIndex < 0) return line;

            return line.Substring(terminatorIndex + Terminator.Length);
        }
    }
}
