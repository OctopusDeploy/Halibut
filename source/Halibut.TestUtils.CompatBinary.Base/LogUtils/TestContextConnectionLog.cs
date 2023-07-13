using System;
using System.Collections.Generic;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Logging;

namespace Halibut.TestUtils.SampleProgram.Base.LogUtils
{
    internal class TestContextConnectionLog : ILog
    {
        readonly string endpoint;
        readonly string name;
        readonly LogLevel logLevel;

        public TestContextConnectionLog(string endpoint, string name, LogLevel logLevel)
        {
            this.endpoint = endpoint;
            this.name = name;
            this.logLevel = logLevel;
        }

        public void Write(EventType type, string message, params object[] args)
        {
            WriteInternal(new LogEvent(type, message, null, args));
        }

        public void WriteException(EventType type, string message, Exception ex, params object[] args)
        {
            WriteInternal(new LogEvent(type, message, ex, args));
        }

        public IList<LogEvent> GetLogs()
        {
            throw new NotImplementedException();
        }

        void WriteInternal(LogEvent logEvent)
        {
            var logEventLogLevel = GetLogLevel(logEvent);

            if (logEventLogLevel >= logLevel)
            {
                var logMessage = string.Format("{5, 16}: {0}:{1} {2}  {3} {4}", logEvent.Type, logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage, name);
                Console.WriteLine(logMessage);
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
    }
}