#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    /// <summary>
    /// LEV-2306 repro. Starts the 5.0.236 compat binary over and over, the way the test harness does,
    /// and records every start where "RunningAndReady" does not reach the test within 30 seconds.
    ///
    /// Each start uses one of several stdout readers, chosen round-robin so they all see the same load:
    /// - CliWrapToDelegate: exactly what HalibutTestBinaryRunner / ProxyHalibutTestBinaryRunner use today.
    /// - LegacyCharReader: CliWrap 3.8.2's line splitting (char-chunk ReadAsync), still inside CliWrap 3.10.5.
    /// - DedicatedThreadReader: a blocking StreamReader.ReadLine loop on its own thread.
    ///
    /// The compat process writes its own timestamped trace of every console write, so a stall can be
    /// classified as "compat never wrote the line" or "compat wrote it but the test never read it".
    /// </summary>
    [Category("CompatStartupStallRepro")]
    public class CompatStartupStallReproFixture : BaseTest
    {
        static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

        enum ReaderArm
        {
            CliWrapToDelegate,
            LegacyCharReader,
            DedicatedThreadReader
        }

        class ArmStats
        {
            public int Starts;
            public int Stalls;
            public readonly ConcurrentBag<double> ReadyMillis = new();
        }

        [Test]
        [TestTimeout(75 * 60)]
        public async Task CompatBinaryStartupReadinessIsAlwaysObserved()
        {
            var durationSetting = Environment.GetEnvironmentVariable("HALIBUT_REPRO_DURATION_MINUTES");
            if (string.IsNullOrEmpty(durationSetting)) Assert.Ignore("LEV-2306 repro: set HALIBUT_REPRO_DURATION_MINUTES to run this stress test.");
            var duration = TimeSpan.FromMinutes(Math.Min(60, double.Parse(durationSetting!)));
            var workers = int.Parse(Environment.GetEnvironmentVariable("HALIBUT_REPRO_WORKERS") ?? "12");
            var arms = (Environment.GetEnvironmentVariable("HALIBUT_REPRO_ARMS") ?? "CliWrapToDelegate,LegacyCharReader,DedicatedThreadReader")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => (ReaderArm)Enum.Parse(typeof(ReaderArm), a.Trim()))
                .ToArray();

            ThreadPool.GetMinThreads(out var minWorker, out var minIo);
            ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
            Logger.Information("REPRO config: duration={Duration} workers={Workers} arms={Arms} cpus={Cpus} framework={Framework} minThreads={MinWorker}/{MinIo} maxThreads={MaxWorker}/{MaxIo}",
                duration, workers, string.Join(",", arms), Environment.ProcessorCount, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, minWorker, minIo, maxWorker, maxIo);

            var stats = arms.ToDictionary(a => a, _ => new ArmStats());
            var stallReports = new ConcurrentQueue<string>();
            var deadline = DateTime.UtcNow + duration;
            var startCounter = 0;

            var workerTasks = Enumerable.Range(0, workers).Select(workerId => Task.Run(async () =>
            {
                while (DateTime.UtcNow < deadline)
                {
                    var n = Interlocked.Increment(ref startCounter);
                    var arm = arms[n % arms.Length];
                    var armStats = stats[arm];
                    Interlocked.Increment(ref armStats.Starts);

                    try
                    {
                        var report = await StartOnce(n, workerId, arm, armStats);
                        if (report != null)
                        {
                            Interlocked.Increment(ref armStats.Stalls);
                            stallReports.Enqueue(report);
                            Logger.Error("REPRO STALL\n{Report}", report);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "REPRO start {N} on worker {Worker} ({Arm}) threw", n, workerId, arm);
                    }
                }
            })).ToArray();

            await Task.WhenAll(workerTasks);

            var summary = new StringBuilder();
            summary.AppendLine("REPRO SUMMARY");
            foreach (var arm in arms)
            {
                var s = stats[arm];
                var ready = s.ReadyMillis.OrderBy(x => x).ToArray();
                double P(double q) => ready.Length == 0 ? 0 : ready[(int)Math.Min(ready.Length - 1, Math.Floor(q * ready.Length))];
                summary.AppendLine($"  {arm}: starts={s.Starts} stalls={s.Stalls} readyMs p50={P(0.5):F0} p99={P(0.99):F0} max={(ready.Length == 0 ? 0 : ready.Last()):F0}");
                Console.WriteLine($"##teamcity[buildStatisticValue key='repro.{arm}.starts' value='{s.Starts}']");
                Console.WriteLine($"##teamcity[buildStatisticValue key='repro.{arm}.stalls' value='{s.Stalls}']");
            }

            Logger.Information(summary.ToString());

            if (!stallReports.IsEmpty)
            {
                Assert.Fail(summary + "\n\n" + string.Join("\n\n", stallReports.Take(10)));
            }
        }

        async Task<string?> StartOnce(int n, int workerId, ReaderArm arm, ArmStats armStats)
        {
            using var stayAlive = new CompatBinaryStayAlive(Logger);
            using var tmp = new TmpDirectory();
            var traceFile = Path.Combine(tmp.FullPath, "compat-console-trace.txt");

            var settings = new Dictionary<string, string?>
            {
                { "mode", "serviceonly" },
                { "tentaclecertpath", Certificates.TentacleListeningPfxPath },
                { "octopusthumbprint", Certificates.OctopusPublicThumbprint },
                { "halibutloglevel", "Trace" },
                { "WithStandardServices", "True" },
                { "WithCachingService", "False" },
                { "WithTentacleServices", "False" },
                { "TestTimeout", TimeSpan.FromMinutes(5).ToString() },
                { "ServiceConnectionType", ServiceConnectionType.Listening.ToString() },
                { CompatBinaryStayAlive.StayAliveFilePathEnvVarKey, stayAlive.LockFile },
                { "HALIBUT_REPRO_COMPAT_TRACE_FILE", traceFile },
                { "HALIBUT_REPRO_BLOCKING_CONSOLE", Environment.GetEnvironmentVariable("HALIBUT_REPRO_BLOCKING_CONSOLE") ?? "1" }
            };

            var received = new ConcurrentQueue<string>();
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            long handlerEnteredTicks = 0;
            string? lineInHandler = null;

            // Same work per line as ProcessLogs in the runners: log through Serilog, then look for the readiness marker.
            void HandleLine(string line)
            {
                Interlocked.Exchange(ref handlerEnteredTicks, DateTime.UtcNow.Ticks);
                lineInHandler = line;
                received.Enqueue(DateTime.UtcNow.ToString("HH:mm:ss.fffffff") + " " + line);
                Logger.Information(line);
                if (line.Contains("RunningAndReady")) ready.TrySetResult(true);
                lineInHandler = null;
                Interlocked.Exchange(ref handlerEnteredTicks, 0);
            }

            var target = arm switch
            {
                ReaderArm.CliWrapToDelegate => PipeTarget.ToDelegate(async (s, ct) =>
                {
                    await Task.CompletedTask;
                    HandleLine(s);
                }),
                ReaderArm.LegacyCharReader => PipeTarget.Create(async (stream, ct) =>
                {
                    using var reader = new StreamReader(stream, Encoding.Default, false, 1024, true);
                    await foreach (var line in LegacyReadAllLinesAsync(reader, ct).ConfigureAwait(false))
                    {
                        HandleLine(line);
                    }
                }),
                ReaderArm.DedicatedThreadReader => PipeTarget.Create((stream, ct) =>
                {
                    var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            using var reader = new StreamReader(stream, Encoding.Default, false, 1024, true);
                            string? line;
                            while ((line = reader.ReadLine()) != null) HandleLine(line);
                            done.TrySetResult(true);
                        }
                        catch (Exception e)
                        {
                            done.TrySetException(e);
                        }
                    }) { IsBackground = true, Name = $"repro-stdout-{n}" };
                    thread.Start();
                    return done.Task;
                }),
                _ => throw new ArgumentOutOfRangeException(nameof(arm))
            };

            using var cts = new CancellationTokenSource();
            var started = Stopwatch.StartNew();
            var startedAt = DateTime.UtcNow;

            var commandTask = Cli.Wrap(new HalibutTestBinaryPath().BinPath("5.0.236"))
                .WithWorkingDirectory(tmp.FullPath)
                .WithEnvironmentVariables(settings)
                .WithStandardOutputPipe(target)
                .WithStandardErrorPipe(target)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cts.Token);

            var pid = commandTask.ProcessId;
            var running = Task.Run(async () =>
            {
                try { return (await commandTask).ExitCode; }
                catch (OperationCanceledException) { return (int?)null; }
            });

            var completed = await Task.WhenAny(ready.Task, running, Task.Delay(StartupTimeout));
            string? report = null;

            if (completed == ready.Task)
            {
                armStats.ReadyMillis.Add(started.Elapsed.TotalMilliseconds);
            }
            else
            {
                report = BuildStallReport(n, workerId, arm, pid, startedAt, started.Elapsed, completed == running, running, traceFile, received, handlerEnteredTicks, lineInHandler);
            }

#if NET8_0_OR_GREATER
            await cts.CancelAsync();
#else
            cts.Cancel();
#endif
            await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(20)));
            return report;
        }

        static string BuildStallReport(int n, int workerId, ReaderArm arm, int pid, DateTime startedAt, TimeSpan elapsed, bool exited, Task<int?> running,
            string traceFile, ConcurrentQueue<string> received, long handlerEnteredTicks, string? lineInHandler)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"start={n} worker={workerId} arm={arm} pid={pid} startedAt={startedAt:HH:mm:ss.fffffff} elapsed={elapsed}");

            bool? alive = null;
            try { alive = !Process.GetProcessById(pid).HasExited; }
            catch (ArgumentException) { alive = false; }
            sb.AppendLine($"processExitedBeforeTimeout={exited} exitCode={(running.IsCompleted ? running.Result?.ToString() ?? "cancelled" : "running")} aliveNow={alive}");

            ThreadPool.GetAvailableThreads(out var availWorker, out var availIo);
            ThreadPool.GetMinThreads(out var minWorker, out var minIo);
            ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
            sb.AppendLine($"threadPool busyWorker={maxWorker - availWorker} busyIo={maxIo - availIo} min={minWorker}/{minIo} processThreads={Process.GetCurrentProcess().Threads.Count}");

            var entered = Interlocked.Read(ref handlerEnteredTicks);
            sb.AppendLine(entered == 0
                ? "lineHandler=idle (reader is not inside our delegate)"
                : $"lineHandler=BUSY since {new DateTime(entered, DateTimeKind.Utc):HH:mm:ss.fffffff} on line '{lineInHandler}'");

            sb.AppendLine("--- lines received by the test (receive time) ---");
            foreach (var line in received) sb.AppendLine(line);

            sb.AppendLine("--- console writes made by the compat process (write time) ---");
            try
            {
                using var fs = new FileStream(traceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var r = new StreamReader(fs);
                sb.AppendLine(r.ReadToEnd());
            }
            catch (Exception e)
            {
                sb.AppendLine("could not read compat trace: " + e.Message);
            }

            return sb.ToString();
        }

        // Copy of CliWrap 3.8.2's StreamExtensions.ReadAllLinesAsync.
        static async IAsyncEnumerable<string> LegacyReadAllLinesAsync(StreamReader reader, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lineBuffer = new StringBuilder();
            var buffer = new char[1024];
            var isLastCaretReturn = false;
            while (true)
            {
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (charsRead <= 0) break;
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 0; i < charsRead; i++)
                {
                    var c = buffer[i];
                    if (isLastCaretReturn && c == '\n')
                    {
                        isLastCaretReturn = false;
                        continue;
                    }

                    if (c is '\n' or '\r')
                    {
                        yield return lineBuffer.ToString();
                        lineBuffer.Clear();
                    }
                    else
                    {
                        lineBuffer.Append(c);
                    }

                    isLastCaretReturn = c == '\r';
                }
            }

            if (lineBuffer.Length > 0) yield return lineBuffer.ToString();
        }
    }
}
