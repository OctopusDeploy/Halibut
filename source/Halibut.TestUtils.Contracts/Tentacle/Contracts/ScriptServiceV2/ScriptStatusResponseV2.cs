using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public class ScriptStatusResponseV2
    {
        public ScriptStatusResponseV2(ScriptTicket ticket,
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