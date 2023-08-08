using System;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientScriptServiceV2
    {
        Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command);
        Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request);
        Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command);
        Task CompleteScriptAsync(CompleteScriptCommandV2 command);
    }
}