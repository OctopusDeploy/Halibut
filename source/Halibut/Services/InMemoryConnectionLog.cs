using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Halibut.Services
{
    public class InMemoryConnectionLog : ILog
    {
        readonly string endpoint;
        readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();
        readonly TraceSource traceSource = new TraceSource("Halibut");

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
            if (traceSource.Switch.ShouldTrace(TraceEventType.Information))
            {
                traceSource.TraceInformation(string.Format("{0,-30} {1,4}  {2} {3}{4}", endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.Type, logEvent.FormattedMessage, (logEvent.Error == null ? "" : Environment.NewLine + logEvent.Error)));
            }
            events.Enqueue(logEvent);

            LogEvent ignore;
            while (events.Count > 100 && events.TryDequeue(out ignore)) { }
        }
    }
}