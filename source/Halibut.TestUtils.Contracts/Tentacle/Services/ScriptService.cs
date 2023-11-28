using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Octopus.Tentacle.Contracts;

namespace Halibut.TestUtils.Contracts.Tentacle.Services
{
    public class ScriptService : IScriptService
    {
        static readonly ConcurrentDictionary<ScriptTicket, RunningScript> scripts = new();

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            SimulateSmallDelay();

            var scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
            var runningScript = new RunningScript
            {
                StartScriptCommand = command,
                FullScriptOutput = command.ScriptBody,
                RemainingGetStatusCallsBeforeComplete = 10,
                ExitCode = 0,
                DesiredExitCode = 0,
                ProcessState = ProcessState.Running,
                ScriptTicket = scriptTicket
            };

            scripts.TryAdd(scriptTicket, runningScript);

            return scriptTicket;
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            SimulateSmallDelay();

            scripts.TryGetValue(request.Ticket, out var runningScript);

            if (runningScript != null)
            {
                var (logs, nextSequenceNumber) = runningScript.GetStatusOrCancelCalled((int)request.LastLogSequence);
                return new ScriptStatusResponse(request.Ticket, runningScript.ProcessState, runningScript.ExitCode, logs, nextSequenceNumber);
            }

            return new ScriptStatusResponse(request.Ticket, ProcessState.Complete, 0, new List<ProcessOutput>(), 0);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            SimulateSmallDelay();

            scripts.TryGetValue(command.Ticket, out var runningScript);

            if (runningScript != null)
            {
                var (logs, nextSequenceNumber) = runningScript.GetStatusOrCancelCalled((int)command.LastLogSequence);
                return new ScriptStatusResponse(command.Ticket, runningScript.ProcessState, runningScript.ExitCode, logs, nextSequenceNumber);
            }

            return new ScriptStatusResponse(command.Ticket, ProcessState.Complete, 0, new List<ProcessOutput>(), 0);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            SimulateSmallDelay();

            scripts.TryGetValue(command.Ticket, out var runningScript);
            scripts.TryRemove(command.Ticket, out _);

            if (runningScript != null)
            {
                var (logs, nextSequenceNumber) = runningScript.CompleteCalled((int)command.LastLogSequence);
                return new ScriptStatusResponse(command.Ticket, runningScript.ProcessState, runningScript.ExitCode, logs, nextSequenceNumber);
            }

            return new ScriptStatusResponse(command.Ticket, ProcessState.Complete, 0, new List<ProcessOutput>(), 0);
        }

        static void SimulateSmallDelay()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(new Random().Next(200, 2000)));
        }
        class RunningScript
        {
            public StartScriptCommand? StartScriptCommand { get; set; }
            public string? FullScriptOutput { get; set; }
            public ScriptTicket? ScriptTicket { get; set; }
            public ProcessState ProcessState { get; set; }
            public int ExitCode { get; set; }
            public int RemainingGetStatusCallsBeforeComplete { get; set; }
            public int DesiredExitCode { get; set; }

            public (List<ProcessOutput> logs, int nextSequenceNumber) GetStatusOrCancelCalled(int lastLogSequence)
            {
                var logLines = FullScriptOutput!.Split('\n');
                var take = new Random().Next(1, 20);
                var logs = logLines.Skip(lastLogSequence).Take(take).Select(x => new ProcessOutput(ProcessOutputSource.StdOut, x.Trim('\r', '\n'))).ToList();
                --RemainingGetStatusCallsBeforeComplete;

                if (RemainingGetStatusCallsBeforeComplete <= 0)
                {
                    ExitCode = DesiredExitCode;
                    ProcessState = ProcessState.Complete;
                }

                return (logs, lastLogSequence + logs.Count);
            }

            public (List<ProcessOutput> logs, int nextSequenceNumber) CompleteCalled(int lastLogSequence)
            {
                var logLines = FullScriptOutput!.Split('\n');
                var logs = logLines.Skip(lastLogSequence).Select(x => new ProcessOutput(ProcessOutputSource.StdOut, x.Trim('\r', '\n'))).ToList();
                
                return (logs, lastLogSequence + logs.Count);
            }
        }
    }
}
