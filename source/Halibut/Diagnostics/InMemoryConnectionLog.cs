using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Halibut.Logging;

namespace Halibut.Diagnostics
{
    public class InMemoryConnectionLog : ILog
    {
        readonly string endpoint;
        readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();
        WeakReference<Logging.ILog> loggerRef;

        public InMemoryConnectionLog(string endpoint)
        {
            this.endpoint = endpoint;
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
            Stopwatch stopwatch = Stopwatch.StartNew();

            var logger = GetLogger();//LogProvider.GetLogger("Halibut");
            stopwatch.Stop();
            Interlocked.Add(ref HalibutLimits.time, ((long) stopwatch.Elapsed.TotalMilliseconds));
            HalibutLimits.lastTime = stopwatch.Elapsed.TotalMilliseconds;
            logger.Log(level, () => "{0,-30} {1,4}  {2}", logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage);
        }

        private Logging.ILog GetLogger()
        {
            if (loggerRef == null)
            {
                Logging.ILog log = LogProvider.GetLogger("Halibut");
                loggerRef = new WeakReference<Logging.ILog>(log);
            }

            if (loggerRef.TryGetTarget(out var logger))
            {
                return logger;
            }
            else
            {
                Logging.ILog log = LogProvider.GetLogger("Halibut");
                loggerRef.SetTarget(log);
                return log;

            }
            
        }

        
    }
}