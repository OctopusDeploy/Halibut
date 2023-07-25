using System;
using System.Collections.Generic;
using Halibut.Diagnostics;

namespace Halibut.Tests.Support.Logging
{
    public class AggregateILog : ILog
    {
        ILog[] logImplementation;

        public AggregateILog(ILog[] logImplementation)
        {
            this.logImplementation = logImplementation;
        }

        public void Write(EventType type, string message, params object[] args)
        {
            foreach (var log in logImplementation)
            {
                log.Write(type, message, args);
            }

            
        }

        public void WriteException(EventType type, string message, Exception ex, params object[] args)
        {
            foreach (var log in logImplementation)
            {
                log.WriteException(type, message, ex, args);
            }
        }

        public IList<LogEvent> GetLogs()
        {
            throw new NotImplementedException();
        }
    }
}