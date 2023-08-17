using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.NUnit;

namespace Halibut.Tests.Support
{
    public class SerilogLoggerBuilder
    {
        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = "";
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                testName = "{TestHash} ";
            }

            var outputTemplate = 
                testName
                + "{Timestamp:HH:mm:ss.fff zzz} "
                + "{ShortContext} "
                + "{Message}{NewLine}{Exception}";

            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(outputTemplate)))
                .Enrich.WithProperty("TestHash", CurrentTestHash())
                .CreateLogger();

            if (TeamCityDetection.IsRunningInTeamCity())
            {
                if (!HasLoggedTestHash.Contains(TestContext.CurrentContext.Test.Name))
                {
                    HasLoggedTestHash.Add(TestContext.CurrentContext.Test.Name);
                    logger.Information($"Test: {TestContext.CurrentContext.Test.Name} has hash {CurrentTestHash()}");
                }
            }

            return logger;
        }

        public static string CurrentTestHash()
        {
            using (SHA256 mySHA256 = SHA256.Create())
            {
                return Convert.ToBase64String(mySHA256.ComputeHash(TestContext.CurrentContext.Test.Name.GetUTF8Bytes()))
                    .Replace("=", "")
                    .Replace("+", "")
                    .Replace("/", "")
                    .Substring(0, 10); // 64 ^ 10 is a big number, most likely we wont have collisions.
            }
        }

        public static ConcurrentBag<string> HasLoggedTestHash = new();

        public class NonProgressNUnitSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter _formatter;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter) => _formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof(formatter));

            static Lazy<bool> IsForcingContextWrite = new(() => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Force_Test_Context_Write")));

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
                
                
                _formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress

                var logLine = output.ToString();
                if (TeamCityDetection.IsRunningInTeamCity() || IsForcingContextWrite.Value)
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
    }
}