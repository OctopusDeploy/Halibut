using Octopus.Tentacle.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateScriptService : IScriptService
    {
        readonly IScriptService service;

        public DelegateScriptService(IScriptService service)
        {
            this.service = service;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            return service.StartScript(command);
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            return service.GetStatus(request);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            return service.CancelScript(command);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            return service.CompleteScript(command);
        }
    }
}
