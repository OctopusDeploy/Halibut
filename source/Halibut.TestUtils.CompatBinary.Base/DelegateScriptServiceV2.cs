using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateScriptServiceV2 : IScriptServiceV2
    {
        readonly IScriptServiceV2 service;

        public DelegateScriptServiceV2(IScriptServiceV2 service)
        {
            this.service = service;
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            return service.StartScript(command);
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            return service.GetStatus(request);
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            return service.CancelScript(command);
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            service.CompleteScript(command);
        }
    }
}
