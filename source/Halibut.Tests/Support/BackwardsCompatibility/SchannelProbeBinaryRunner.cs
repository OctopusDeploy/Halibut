using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Halibut.Logging;
using Serilog;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class SchannelProbeBinaryRunner
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly int? clientListenPort;
        readonly CertAndThumbprint clientCertAndThumbprint;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly ILogger logger;

        /// <summary>
        /// Launches the SchannelProbe binary as a listening tentacle (server dials it) or a
        /// polling tentacle (it dials the server). Uses the current version of Halibut.
        /// </summary>
        public SchannelProbeBinaryRunner(
            ServiceConnectionType serviceConnectionType,
            int? clientListenPort,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint,
            ILogger logger)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientListenPort = clientListenPort;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.logger = logger.ForContext<SchannelProbeBinaryRunner>();
        }

        public async Task<RunningSchannelProbe> Run()
        {
            var compatBinaryStayAlive = new CompatBinaryStayAlive(logger);

            var settings = new Dictionary<string, string?>
            {
                { "mode", "serviceonly" },
                { "tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath },
                { "octopusthumbprint", clientCertAndThumbprint.Thumbprint },
                { "halibutloglevel", LogLevel.Info.ToString() },
                { CompatBinaryStayAlive.StayAliveFilePathEnvVarKey, compatBinaryStayAlive.LockFile },
                { "WithStandardServices", true.ToString() },
                { "WithCachingService", false.ToString() },
                { "WithTentacleServices", false.ToString() },
                { "ServiceConnectionType", serviceConnectionType.ToString() },
            };

            if (serviceConnectionType == ServiceConnectionType.Polling && clientListenPort.HasValue)
                settings.Add("octopusservercommsport", "https://localhost:" + clientListenPort.Value);

            var cts = new CancellationTokenSource();
            var hasStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int? serviceListenPort = null;

            var runningTask = Task.Run(async () =>
            {
                try
                {
                    await Cli.Wrap(new HalibutTestBinaryPath().SchannelProbeBinPath())
                        .WithEnvironmentVariables(settings)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate((line, _) =>
                        {
                            logger.Information(line);
                            if (line.StartsWith("Listening on port: "))
                                serviceListenPort = int.Parse(Regex.Match(line, @"\d+").Value);
                            if (line.Contains("RunningAndReady"))
                                hasStarted.TrySetResult(true);
                            return Task.CompletedTask;
                        }))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate((line, _) =>
                        {
                            logger.Information(line);
                            return Task.CompletedTask;
                        }))
                        .ExecuteAsync(cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    hasStarted.TrySetException(e);
                    throw;
                }
            });

            var winner = await Task.WhenAny(runningTask, hasStarted.Task, Task.Delay(TimeSpan.FromSeconds(30)));

            if (winner == runningTask || !hasStarted.Task.IsCompleted)
            {
                cts.Cancel();
                cts.Dispose();
                compatBinaryStayAlive.Dispose();
                if (winner == runningTask) await runningTask; // re-throw startup exception
                throw new Exception("SchannelProbe binary did not start within 30 seconds");
            }

            return new RunningSchannelProbe(cts, serviceListenPort, compatBinaryStayAlive);
        }

        public class RunningSchannelProbe : IDisposable
        {
            readonly CancellationTokenSource cts;
            readonly CompatBinaryStayAlive compatBinaryStayAlive;

            public int? ServiceListenPort { get; }

            public RunningSchannelProbe(CancellationTokenSource cts, int? serviceListenPort, CompatBinaryStayAlive compatBinaryStayAlive)
            {
                this.cts = cts;
                this.compatBinaryStayAlive = compatBinaryStayAlive;
                ServiceListenPort = serviceListenPort;
            }

            public void Dispose()
            {
                cts.Cancel();
                cts.Dispose();
                compatBinaryStayAlive.Dispose();
            }
        }
    }
}
