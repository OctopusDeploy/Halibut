using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Halibut.Tests.Support
{
    public class SerilogLoggerBuilder
    {
        static readonly ILogger Logger;
        static readonly ConcurrentDictionary<string, TraceLogFileLogger> TraceLoggers = new();
        static readonly ConcurrentBag<string> HasLoggedTestHash = new();

        TraceLogFileLogger? traceFileLogger;

        static SerilogLoggerBuilder()
        {
            const string teamCityOutputTemplate =
                "{TestHash} "
                + "{Timestamp:HH:mm:ss.fff zzz} "
                + "{ShortContext} "
                + "{Message}{NewLine}{Exception}";

            const string localOutputTemplate =
                "{Timestamp:HH:mm:ss.fff zzz} "
                + "{ShortContext} "
                + "{Message}{NewLine}{Exception}";

            var nUnitOutputTemplate = TeamCityDetection.IsRunningInTeamCity()
                ? teamCityOutputTemplate
                : localOutputTemplate;

            Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(nUnitOutputTemplate)), LogEventLevel.Debug)
                .WriteTo.Sink(new TraceLogsForFailedTestsSink(new MessageTemplateTextFormatter(localOutputTemplate)))
                .CreateLogger();
        }

        public SerilogLoggerBuilder SetTraceLogFileLogger(TraceLogFileLogger logger)
        {
            this.traceFileLogger = logger;
            return this;
        }

        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = TestContext.CurrentContext.Test.FullName;
            var testHash = CurrentTestHash();
            var logger = Logger.ForContext("TestHash", testHash);

            if (!HasLoggedTestHash.Contains(testName))
            {
                HasLoggedTestHash.Add(testName);
                logger.Information($"Test: {TestContext.CurrentContext.Test.Name} has hash {testHash}");
            }

            if (traceFileLogger != null)
            {
                TraceLoggers.AddOrUpdate(testName, traceFileLogger, (_, _) => throw new Exception("This should never be updated. If it is, it means that a test is being run multiple times in a single test run"));
            }

            return logger;
        }

        public static string CurrentTestHash()
        {
            using (SHA256 mySHA256 = SHA256.Create())
            {
                string s = TestContext.CurrentContext.Test.FullName;
#if NETFRAMEWORK
                // Allow net6 and net48 tests to run at the same time by giving them different test hashes.
                s += "Net48";
#endif
                return Convert.ToBase64String(mySHA256.ComputeHash(s.GetUTF8Bytes()))
                    .Replace("=", "")
                    .Replace("+", "")
                    .Replace("/", "")
                    .Substring(0, 10); // 64 ^ 10 is a big number, most likely we wont have collisions.
            }
        }

        public class NonProgressNUnitSink : ILogEventSink
        {
            readonly MessageTemplateTextFormatter formatter;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter) => this.formatter = formatter;

            static bool IsForcingContextWrite = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Force_Test_Context_Write"));

            public void Emit(LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));
                if (TestContext.Out == null)
                    return;
                var output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    var context = sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "");
                    //output.Write("[" + context + "] ");

                    logEvent.AddOrUpdateProperty(new LogEventProperty("ShortContext", new ScalarValue(context)));
                }

                formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress

                var logLine = output.ToString();
                if (TeamCityDetection.IsRunningInTeamCity() || IsForcingContextWrite)
                {
                    // Writing to TestContext doesn't seem to result in the output showing up under the test in TeamCity.
                    TestContext.Write(logLine);
                }
                else
                {
                    TestContext.Progress.Write(logLine);
                }
            }
        }

        public class TraceLogsForFailedTestsSink : ILogEventSink
        {
            readonly MessageTemplateTextFormatter formatter;

            public TraceLogsForFailedTestsSink(MessageTemplateTextFormatter formatter) => this.formatter = formatter;

            public void Emit(LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));

                var testName = TestContext.CurrentContext.Test.FullName;

                if (!TraceLoggers.TryGetValue(testName, out var traceLogger))
                    throw new Exception($"Could not find trace logger for test '{testName}'");

                var output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    var context = sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "");
                    logEvent.AddOrUpdateProperty(new LogEventProperty("ShortContext", new ScalarValue(context)));
                }

                formatter.Format(logEvent, output);

                var logLine = output.ToString().Trim();
                traceLogger.WriteLine(logLine);
            }
        }
    }
}
