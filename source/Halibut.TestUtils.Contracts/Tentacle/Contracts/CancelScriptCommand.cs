using System;

namespace Octopus.Tentacle.Contracts
{
    public class CancelScriptCommand
    {
        public CancelScriptCommand(ScriptTicket ticket, long lastLogSequence)
        {
            Ticket = ticket;
            LastLogSequence = lastLogSequence;
        }

        public ScriptTicket Ticket { get; }

        public long LastLogSequence { get; }
    }
}