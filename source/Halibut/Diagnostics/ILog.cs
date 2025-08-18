using System;
using System.Collections.Generic;
using Halibut.Diagnostics.LogWriters;

namespace Halibut.Diagnostics
{
    public interface ILog : ILogWriter
    {
        IList<LogEvent> GetLogs();

        ILog ForContext<T>();
    }
}