using System;
using System.Collections.Generic;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogWriters;
using Halibut.Logging;
using Serilog.Events;
using ILog = Halibut.Diagnostics.ILog;
using LogEvent = Halibut.Diagnostics.LogEvent;

namespace Halibut.Tests.Support.Logging
{
    public class TestContextConnectionLog : ILog, ILogWriter
    {
        readonly string endpoint;
        readonly string name;
        readonly LogLevel logLevel;
        readonly Type? forContextType;

        public TestContextConnectionLog(string endpoint, string name, LogLevel logLevel, Type? forContextType = null)
        {
            this.endpoint = endpoint;
            this.name = name;
            this.logLevel = logLevel;
            this.forContextType = forContextType;
        }

        public void Write(EventType type, string message, params object?[] args)
        {
            WriteInternal(new LogEvent(type, message, null, args));
        }

        public void WriteException(EventType type, string message, Exception ex, params object?[] args)
        {
            WriteInternal(new LogEvent(type, message, ex, args));
        }

        public IList<LogEvent> GetLogs()
        {
            throw new NotImplementedException();
        }

        public ILog ForContext<T>()
        {
            return new TestContextConnectionLog(endpoint, this.name, logLevel, typeof(T));
        }

        void WriteInternal(LogEvent logEvent)
        {
            var logEventLogLevel = GetLogLevel(logEvent);

            if (logEventLogLevel >= logLevel)
            {
                var builder = new SerilogLoggerBuilder().Build();
                var methodLogger = forContextType != null ? builder.ForContext(forContextType) : builder.ForContext<TestContextConnectionLog>();
                methodLogger.Write(GetSerilogLevel(logEvent), string.Format("{5, 16}: {0}:{1} {2}  {3} {4}", logEventLogLevel, logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage, name));
            }
        }

        static LogLevel GetLogLevel(LogEvent logEvent)
        {
            switch (logEvent.Type)
            {
                case EventType.Error:
                    return LogLevel.Error;
                case EventType.Diagnostic:
                case EventType.SecurityNegotiation:
                case EventType.MessageExchange:
                    return LogLevel.Trace;
                case EventType.OpeningNewConnection:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }

        static LogEventLevel GetSerilogLevel(LogEvent logEvent)
        {
            switch (logEvent.Type)
            {
                case EventType.Error:
                    return LogEventLevel.Error;
                case EventType.Diagnostic:
                case EventType.SecurityNegotiation:
                case EventType.MessageExchange:
                    return LogEventLevel.Verbose;
                case EventType.OpeningNewConnection:
                    return LogEventLevel.Debug;
                default:
                    return LogEventLevel.Information;
            }
        }
    }
}
