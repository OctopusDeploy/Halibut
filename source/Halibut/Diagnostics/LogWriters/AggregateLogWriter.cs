using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Diagnostics.LogWriters
{
    /// <summary>
    /// An ILog which writes the log events to both an inner ILog and to the set of ILogWriters.   
    /// </summary>
    public class AggregateLogWriter : ILog
    {
        readonly ILog log;
        readonly ILog[] logWriter;

        public AggregateLogWriter(ILog log, ILog[] logWriter)
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

        public ILog ForContext<T>()
        {
            return new AggregateLogWriter(log.ForContext<T>(), logWriter.Select(lw => lw.ForContext<T>()).ToArray());
        }
    }
}