using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts
{
    public enum ProcessState
    {
        Pending,
        Running,
        Complete
    }

    public enum ProcessOutputSource
    {
        StdOut,
        StdErr,
        Debug
    }

    public class ProcessOutput
    {
        public ProcessOutput(ProcessOutputSource source, string text) : this(source, text, DateTimeOffset.UtcNow)
        {
        }

        [JsonConstructor]
        public ProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred)
        {
            Source = source;
            Text = text;
            Occurred = occurred;
        }

        public ProcessOutputSource Source { get; }

        public DateTimeOffset Occurred { get; }

        public string Text { get; }
    }

    public class ScriptStatusResponse
    {
        public ScriptStatusResponse(ScriptTicket ticket,
            ProcessState state,
            int exitCode,
            List<ProcessOutput> logs,
            long nextLogSequence)
        {
            Ticket = ticket;
            State = state;
            ExitCode = exitCode;
            Logs = logs;
            NextLogSequence = nextLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public List<ProcessOutput> Logs { get; }

        public long NextLogSequence { get; }

        public ProcessState State { get; }

        public int ExitCode { get; }
    }
}