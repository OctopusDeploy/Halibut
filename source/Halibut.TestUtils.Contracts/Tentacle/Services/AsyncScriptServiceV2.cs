using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Halibut.TestUtils.Contracts.Tentacle.Services
{
    public class AsyncScriptServiceV2 : IAsyncScriptServiceV2
    {
        static readonly ConcurrentDictionary<ScriptTicket, RunningScript> scripts = new();
        
        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, CancellationToken cancellationToken)
        {
            await SimulateSmallDelayAsync();
            
            var runningScript = new RunningScript
            {
                StartScriptCommand = command,
                FullScriptOutput = command.ScriptBody,
                RemainingGetStatusCallsBeforeComplete = 10,
                ExitCode = 0,
                DesiredExitCode = 0,
                ProcessState = ProcessState.Running,
                ScriptTicket = command.ScriptTicket
            };

            scripts.TryAdd(command.ScriptTicket, runningScript);

            return new ScriptStatusResponseV2(command.ScriptTicket, runningScript.ProcessState, runningScript.ExitCode, runningScript.GetProcessOutput(0), 1);
        }

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, CancellationToken cancellationToken)
        {
            await SimulateSmallDelayAsync();

            scripts.TryGetValue(request.Ticket, out var runningScript);

            if (runningScript != null)
            {
                var (logs, nextSequenceNumber) = runningScript.GetStatusOrCancelCalled((int)request.LastLogSequence);
                return new ScriptStatusResponseV2(request.Ticket, runningScript.ProcessState, runningScript.ExitCode, logs, nextSequenceNumber);
            }

            return new ScriptStatusResponseV2(request.Ticket, ProcessState.Complete, 0, new List<ProcessOutput>(), 0);
        }

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, CancellationToken cancellationToken)
        {
            await SimulateSmallDelayAsync();

            scripts.TryGetValue(command.Ticket, out var runningScript);

            if (runningScript != null)
            {
                var (logs, nextSequenceNumber) = runningScript.GetStatusOrCancelCalled((int)command.LastLogSequence);
                return new ScriptStatusResponseV2(command.Ticket, runningScript.ProcessState, runningScript.ExitCode, logs, nextSequenceNumber);
            }

            return new ScriptStatusResponseV2(command.Ticket, ProcessState.Complete, 0, new List<ProcessOutput>(), 0);
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, CancellationToken cancellationToken)
        {
            await SimulateSmallDelayAsync();

            scripts.TryRemove(command.Ticket, out _);
        }

        static async Task SimulateSmallDelayAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(200, 2000)));
        }

        class RunningScript
        {
            public StartScriptCommandV2 StartScriptCommand { get; set; }
            public string FullScriptOutput { get; set; }
            public ScriptTicket ScriptTicket { get; set; }
            public ProcessState ProcessState { get; set; }
            public int ExitCode { get; set; }
            public int RemainingGetStatusCallsBeforeComplete { get; set; }
            public int DesiredExitCode { get; set; }

            public (List<ProcessOutput> logs, int nextSequenceNumber) GetStatusOrCancelCalled(int lastLogSequence)
            {
                --RemainingGetStatusCallsBeforeComplete;

                if (RemainingGetStatusCallsBeforeComplete <= 0)
                {
                    ExitCode = DesiredExitCode;
                    ProcessState = ProcessState.Complete;
                }

                var logLines = FullScriptOutput.Split('\n');
                var take = ProcessState == ProcessState.Complete ? int.MaxValue : new Random().Next(1, 20);
                var logs = logLines.Skip(lastLogSequence).Take(take).Select(x => new ProcessOutput(ProcessOutputSource.StdOut, x.Trim('\r', '\n'))).ToList();

                return (logs, lastLogSequence + logs.Count);
            }

            public List<ProcessOutput> GetProcessOutput(int index)
            {
                var logLines = FullScriptOutput.Split('\n');

                return new List<ProcessOutput>{ new (ProcessOutputSource.StdOut, logLines.ElementAt(index).Trim('\r', '\n')) };
            }
        }
    }
}
