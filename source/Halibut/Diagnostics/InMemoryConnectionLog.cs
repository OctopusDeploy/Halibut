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
        readonly Logging.ILog? logger;
        readonly ConcurrentQueue<LogEvent> events = new();

        /// <summary>
        /// Writes logs to an in memory queue of events as well as to the logger returned
        /// by LogProvider.GetLogger("Halibut")
        /// </summary>
        /// <param name="endpoint"></param>
        public InMemoryConnectionLog(string endpoint)
        {
            this.endpoint = endpoint;
            this.logger = LogProvider.GetLogger("Halibut");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="logger">When null, this will not write to the Logging.ILog</param>
        public InMemoryConnectionLog(string endpoint, Logging.ILog? logger)
        {
            this.endpoint = endpoint;
            this.logger = logger;
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
            return events.ToArray();
        }

        public ILog ForContext<T>() => this;

        void WriteInternal(LogEvent logEvent)
        {
            SendToTrace(logEvent);

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
                case EventType.ClientDenied:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }

        void SendToTrace(LogEvent logEvent)
        {
            if (logger != null)
            {
                var level = GetLogLevel(logEvent);
                logger.Log(level, () => "{0,-30} {1,4}  {2}", logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage);
            }
        }
    }
}