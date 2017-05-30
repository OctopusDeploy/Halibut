using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Halibut.Logging;

namespace Halibut.Diagnostics
{
    public class InMemoryConnectionLog : ILog
    {
        readonly string endpoint;
        readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();
        readonly Logging.ILog logger = LogProvider.GetLogger("Halibut");

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
            SendToTrace(logEvent, logEvent.Type == EventType.Diagnostic ? LogLevel.Trace : LogLevel.Info);

            events.Enqueue(logEvent);

            LogEvent ignore;
            while (events.Count > 100 && events.TryDequeue(out ignore)) { }
        }

        void SendToTrace(LogEvent logEvent, LogLevel level)
        {
            logger.Log(level, () => "{0,-30} {1,4}  {2}", logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage);
        }
    }
}