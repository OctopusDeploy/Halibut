using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Halibut.Diagnostics
{
    public class InMemoryConnectionLog : ILog
    {
        static readonly TraceSource TraceSource = new TraceSource("Halibut");
        readonly string endpoint;
        readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();
        
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
            SendToTrace(logEvent, logEvent.Type == EventType.Diagnostic ? TraceEventType.Verbose : TraceEventType.Information);

            events.Enqueue(logEvent);

            LogEvent ignore;
            while (events.Count > 100 && events.TryDequeue(out ignore)) { }
        }

        void SendToTrace(LogEvent logEvent, TraceEventType level)
        {
            if (TraceSource.Switch.ShouldTrace(level))
            {
                TraceSource.TraceEvent(level, 0, string.Format("{0,-30} {1,4}  {2}{3}", endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage, (logEvent.Error == null ? "" : Environment.NewLine + logEvent.Error)));
            }
        }
    }
}