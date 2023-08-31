using System;

namespace Halibut.Diagnostics.LogWriters
{
    public interface ILogWriter
    {
        void Write(EventType type, string message, params object[] args);
        void WriteException(EventType type, string message, Exception ex, params object[] args);
    }
}