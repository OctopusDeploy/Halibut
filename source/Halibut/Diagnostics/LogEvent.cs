using System;

namespace Halibut.Diagnostics
{
    public class LogEvent
    {
        public LogEvent(EventType type, string message, Exception error, object[] formatArguments)
        {
            Type = type;
            Error = error;
            Time = DateTimeOffset.UtcNow;
            FormattedMessage = formatArguments == null || formatArguments.Length == 0
                ? message
                : string.Format(message, formatArguments);
        }

        public EventType Type { get; }
        
        public string FormattedMessage { get; }

        public Exception Error { get; }

        public DateTimeOffset Time { get; }

        public override string ToString()
        {
            return Type + " " + FormattedMessage;
        }
    }
}