using System;
using System.IO;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.NUnit;

namespace Halibut.Tests.Util
{
    public class SerilogLoggerBuilder
    {
        public ILogger Build()
        {
            // In teamcity we need to know what test the log is for, since we can find hung builds and only have a single file containing all log messages.
            var testName = "";
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                testName = "[{TestName}] ";
            }
            
            var outputTemplate = "{Timestamp:HH:mm:ss.fff zzz} "
                + testName
                + "{Message}{NewLine}{Exception}";
            
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(new NonProgressNUnitSink(new MessageTemplateTextFormatter(outputTemplate)))
                .CreateLogger();
        }
        
        public class NonProgressNUnitSink : ILogEventSink
        {
            private readonly MessageTemplateTextFormatter _formatter;

            public NonProgressNUnitSink(MessageTemplateTextFormatter formatter) => _formatter = formatter != null ? formatter : throw new ArgumentNullException(nameof(formatter));

            public void Emit(Serilog.Events.LogEvent logEvent)
            {
                if (logEvent == null)
                    throw new ArgumentNullException(nameof(logEvent));
                if (TestContext.Out == null)
                    return;
                var output = new StringWriter();
                if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
                {
                    output.Write("[" + sourceContext.ToString().Substring(sourceContext.ToString().LastIndexOf('.') + 1).Replace("\"", "") + "] ");
                }
                _formatter.Format(logEvent, output);
                // This is the change, call this instead of: TestContext.Progress
                
                var logLine = output.ToString();
                if (TeamCityDetection.IsRunningInTeamCity())
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