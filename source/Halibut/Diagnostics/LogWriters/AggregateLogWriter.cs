using System;
using System.Collections.Generic;

namespace Halibut.Diagnostics.LogWriters
{
    /// <summary>
    /// An ILog which writes the log events to both an inner ILog and to the set of ILogWriters.   
    /// </summary>
    public class AggregateLogWriter : ILog
    {
        readonly ILog log;
        readonly ILogWriter[] logWriter;

        public AggregateLogWriter(ILog log, ILogWriter[] logWriter)
        {
            this.log = log;
            this.logWriter = logWriter;
        }

        public void Write(EventType type, string message, params object?[] args)
        {
            foreach (var writer in logWriter)
            {
                writer.Write(type, message, args);
            }
            log.Write(type, message, args);
        }

        public void WriteException(EventType type, string message, Exception ex, params object?[] args)
        {
            foreach (var writer in logWriter)
            {
                writer.WriteException(type, message, ex, args);
            }
            log.WriteException(type, message, ex, args);
        }

        public IList<LogEvent> GetLogs()
        {
            return log.GetLogs();
        }
    }
}