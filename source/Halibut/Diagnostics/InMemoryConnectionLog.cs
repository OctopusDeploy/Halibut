using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Halibut.Logging;

namespace Halibut.Diagnostics
{
    internal class InMemoryConnectionLog : ILog
    {
        readonly string endpoint;
        readonly Logging.ILog logger;
        readonly ConcurrentQueue<LogEvent> events = new();

        public InMemoryConnectionLog(string endpoint)
        {
            this.endpoint = endpoint;
            this.logger = LogProvider.GetLogger("Halibut");
        }

        public InMemoryConnectionLog(string endpoint, Logging.ILog logger)
        {
            this.endpoint = endpoint;
            this.logger = logger;
        }

        public void Write(EventType type, string message, params object?[] args)
        {
            WriteInternal(new LogEvent(type, message, null!, args));
        }

        public void WriteException(EventType type, string message, Exception ex, params object?[] args)
        {
            WriteInternal(new LogEvent(type, message, ex, args));
        }

        public IList<LogEvent> GetLogs()
        {
            return events.ToArray();
        }

        void WriteInternal(LogEvent logEvent)
        {
            var logLevel = GetLogLevel(logEvent);
            SendToTrace(logEvent, logLevel);

            events.Enqueue(logEvent);

            while (events.Count > 100 && events.TryDequeue(out _)) { }
        }

        static LogLevel GetLogLevel(LogEvent logEvent)
        {
            switch (logEvent.Type)
            {
                case EventType.Error:
                case EventType.ErrorInInitialisation:
                case EventType.ErrorInIdentify:
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

        void SendToTrace(LogEvent logEvent, LogLevel level)
        {
            logger.Log(level, () => "{0,-30} {1,4}  {2}", logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage);
        }
    }
}