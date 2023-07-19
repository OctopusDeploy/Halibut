using System;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public class CompleteScriptCommandV2
    {
        public CompleteScriptCommandV2(ScriptTicket ticket)
        {
            Ticket = ticket;
        }

        public ScriptTicket Ticket { get; }
    }
}