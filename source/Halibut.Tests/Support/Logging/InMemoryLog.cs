using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Halibut.Diagnostics;
using Serilog;

namespace Halibut.Tests.Support.Logging
{
    public class InMemoryLog : ILog
    {
        readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();

        public void Write(EventType type, string message, params object[] args)
        {
            var logEvent = new LogEvent(type, message, null, args);
            WriteInternal(logEvent);
        }

        public void WriteException(EventType type, string message, Exception ex, params object[] args)
        {
            WriteInternal(new LogEvent(type, message, ex, args));
        }

        void WriteInternal(LogEvent logEvent)
        {
            events.Enqueue(logEvent);
        }

        public IList<LogEvent> GetLogs()
        {
            return events.ToArray();
        }
    }
}