using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Contracts
{
    public interface IAsyncScriptService
    {
        Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, CancellationToken cancellationToken);
        Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, CancellationToken cancellationToken);
    }
}