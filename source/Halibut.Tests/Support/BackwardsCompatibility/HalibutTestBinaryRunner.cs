using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Shellfish;
using Serilog;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class HalibutTestBinaryRunner
    {
        // The port the binary should poll.
        readonly ServiceConnectionType serviceConnectionType;
        readonly int? clientServicePort;
        readonly CertAndThumbprint clientCertAndThumbprint;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly string version;
        ILogger logger = new SerilogLoggerBuilder().Build().ForContext<HalibutRuntimeBuilder>();
        readonly Uri webSocketServiceEndpointUri;

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
        }
        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, int? clientServicePort, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version)
        {
            this.clientServicePort = clientServicePort;
        }

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, Uri webSocketServiceEndpointUri, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version)
        {
            this.webSocketServiceEndpointUri = webSocketServiceEndpointUri;
        }
        
        public async Task<RunningOldHalibutBinary> Run()
        {
            var envs = new Dictionary<string, string>();
            envs.Add("mode", "serviceonly");
            envs.Add("tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath);
            envs.Add("octopusthumbprint", clientCertAndThumbprint.Thumbprint);
            if (clientServicePort != null)
            {
                envs.Add("octopusservercommsport", "https://localhost:" + clientServicePort);
            }
            else if (webSocketServiceEndpointUri != null)
            {
                envs.Add("sslthubprint", Certificates.SslThumbprint);
                envs.Add("octopusservercommsport", webSocketServiceEndpointUri.ToString());
            }

            envs.Add("ServiceConnectionType", serviceConnectionType.ToString());

            CancellationTokenSource cts = new CancellationTokenSource();

            try
            {
                var tmp = new TmpDirectory();

                var (task, serviceListenPort) = await StartHalibutTestBinary(version, envs, tmp, cts.Token);

                return new RunningOldHalibutBinary(cts, task, tmp, serviceListenPort);
            }
            catch (Exception)
            {
                cts.Cancel();
                throw;
            }
        }

        async Task<(Task, int?)> StartHalibutTestBinary(string version, Dictionary<string, string> envs, TmpDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            int? serviceListenPort = null;
            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    Action<string> processLogs = s =>
                    {
                        logger.Information(s);
                        if (s.StartsWith("Listening on port: "))
                        {
                            serviceListenPort = int.Parse(Regex.Match(s, @"\d+").Value);
                        }

                        if (s.Contains("RunningAndReady")) hasTentacleStarted.Set();
                    };

                    ShellExecutor.ExecuteCommand(new HalibutTestBinaryPath().BinPath(version),
                        "",
                        tmp.FullPath,
                        processLogs,
                        processLogs,
                        processLogs,
                        customEnvironmentVariables: envs,
                        cancel: cancellationToken
                    );
                }
                catch (Exception e)
                {
                    logger.Information(e, "Error running Halibut Test Binary");
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, Task.Run(() => { hasTentacleStarted.WaitHandle.WaitOne(TimeSpan.FromMinutes(1)); }));

            // Will throw.
            if (runningTentacle.IsCompleted) await runningTentacle;

            if (!hasTentacleStarted.IsSet)
            {
                throw new Exception("Halibut test binary did not appear to start correctly");
            }

            return (runningTentacle, serviceListenPort);
        }

        public class RunningOldHalibutBinary : IDisposable
        {
            readonly CancellationTokenSource cts;
            readonly Task runningOldHalibutTask;
            readonly TmpDirectory tmpDirectory;
            public readonly int? serviceListenPort;

            public RunningOldHalibutBinary(CancellationTokenSource cts, Task runningOldHalibutTask, TmpDirectory tmpDirectory, int? serviceListenPort)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
                this.serviceListenPort = serviceListenPort;
            }

            public void Dispose()
            {
                cts.Cancel();
                runningOldHalibutTask.GetAwaiter().GetResult();
                tmpDirectory.Dispose();
            }
        }
    }
}