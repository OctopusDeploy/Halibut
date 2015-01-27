using System;
using System.Collections.Generic;

namespace Halibut.Diagnostics
{
    public interface ILog
    {
        void Write(EventType type, string message, params object[] args);
        void WriteException(EventType type, string message, Exception ex, params object[] args);
        IList<LogEvent> GetLogs();
    }
}