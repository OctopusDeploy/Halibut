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
        readonly LogEventStorage logEventStorage;

        public InMemoryConnectionLog(string endpoint, LogEventStorage logEventStorage)
        {
            this.endpoint = endpoint;
            this.logEventStorage = logEventStorage;
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
            return logEventStorage.GetLogs(endpoint);
        }

        void WriteInternal(LogEvent logEvent)
        {
            SendToTrace(logEvent, logEvent.Type == EventType.Diagnostic ? LogLevel.Trace : LogLevel.Info);

            logEventStorage.AddLog(endpoint, logEvent);
        }

        void SendToTrace(LogEvent logEvent, LogLevel level)
        {
            var logger = LogProvider.GetLogger("Halibut");
            logger.Log(level, () => "{0,-30} {1,4}  {2}", logEvent.Error, endpoint, Thread.CurrentThread.ManagedThreadId, logEvent.FormattedMessage);
        }
    }
}