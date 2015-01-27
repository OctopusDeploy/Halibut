using System;

namespace Halibut.Services
{
    public class LogEvent
    {
        private readonly EventType type;
        private readonly string message;
        private readonly Exception error;
        private readonly object[] formatArguments;
        private readonly DateTimeOffset time;
        private readonly Lazy<string> formattedMessage;

        public LogEvent(EventType type, string message, Exception error, object[] formatArguments)
        {
            this.type = type;
            this.message = message;
            this.error = error;
            this.formatArguments = formatArguments;
            time = DateTimeOffset.UtcNow;
            formattedMessage = new Lazy<string>(FormatMessage);
        }

        public EventType Type
        {
            get { return type; }
        }

        public string Message
        {
            get { return message; }
        }

        public string FormattedMessage
        {
            get { return formattedMessage.Value; }
        }

        public Exception Error
        {
            get { return error; }
        }

        public object[] FormatArguments
        {
            get { return formatArguments; }
        }

        public DateTimeOffset Time
        {
            get { return time; }
        }

        string FormatMessage()
        {
            return formatArguments == null || formatArguments.Length == 0
                ? Message
                : string.Format(message, formatArguments);
        }

        public override string ToString()
        {
            return Type + " " + Message;
        }
    }
}