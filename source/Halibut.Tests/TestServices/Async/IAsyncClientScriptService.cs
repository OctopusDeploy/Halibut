using System;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientScriptService
    {
        Task<ScriptTicket> StartScriptAsync(StartScriptCommand command);
        Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request);
        Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command);
        Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command);
    }
}