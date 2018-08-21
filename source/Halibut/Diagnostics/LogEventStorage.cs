using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Halibut.Diagnostics
{
    public class LogEventStorage
    {
        static readonly LogEvent[] EmptyLogs = new LogEvent[0];

        readonly ConcurrentDictionary<string, Queue<LogEvent>> events = new ConcurrentDictionary<string, Queue<LogEvent>>();
        readonly Queue<string> lastLogEndpoint = new Queue<string>();

        public LogEventStorage()
        {
        }

        public IList<LogEvent> GetLogs(string endpoint)
        {
            if (events.TryGetValue(endpoint, out var logs))
                return logs.ToArray();

            return EmptyLogs;
        }

        public void AddLog(string endpoint, LogEvent logEvent)
        {
            lock (events)
            {
                var logs = events.GetOrAdd(endpoint, e => new Queue<LogEvent>());
                logs.Enqueue(logEvent);
                lastLogEndpoint.Enqueue(endpoint);

                while (lastLogEndpoint.Count > HalibutLimits.LogStorageLimit)
                {
                    var endPointToDeleteFrom = lastLogEndpoint.Dequeue();
                    if (events.TryGetValue(endPointToDeleteFrom, out var oldLogs))
                    {
                        oldLogs.Dequeue();
                    }
                }
            }
        }
    }
}