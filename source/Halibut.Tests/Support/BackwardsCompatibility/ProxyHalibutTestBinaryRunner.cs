using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shellfish;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class ProxyHalibutTestBinaryRunner
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly int? clientServicePort;
        readonly CertAndThumbprint clientCertAndThumbprint;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly string version;
        readonly Uri realServiceListenAddress;

        public ProxyHalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, int? clientServicePort, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, Uri realServiceListenAddress, string version)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientServicePort = clientServicePort;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
            this.realServiceListenAddress = realServiceListenAddress;
        }


        public async Task<RoundTripRunningOldHalibutBinary> Run()
        {
            var envs = new Dictionary<string, string>();
            envs.Add("mode", "proxy");
            envs.Add("tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath);
            envs.Add("octopuscertpath", clientCertAndThumbprint.CertificatePfxPath);
            if (clientServicePort != null)
            {
                envs.Add("octopusservercommsport", "https://localhost:" + clientServicePort);
            }

            if (realServiceListenAddress != null)
            {
                envs.Add("realServiceListenAddress", realServiceListenAddress.ToString());
            }

            envs.Add("ServiceConnectionType", serviceConnectionType.ToString());

            CancellationTokenSource cts = new CancellationTokenSource();

            try
            {
                var tmp = new TmpDirectory();

                var (task, serviceListenPort, proxyClientListenPort) = await StartHalibutTestBinary(version, envs, tmp, cts.Token);

                return new RoundTripRunningOldHalibutBinary(cts, task, tmp, serviceListenPort, proxyClientListenPort);
            }
            catch (Exception)
            {
                cts.Cancel();
                throw;
            }
        }

        async Task<(Task, int?, int?)> StartHalibutTestBinary(string version, Dictionary<string, string> envs, TmpDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            var logger = new SerilogLoggerBuilder().Build().ForContext<ProxyHalibutTestBinaryRunner>();
            int? serviceListenPort = null;
            int? proxyClientListenPort = null;
            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    Action<string> processLogs = s =>
                    {
                        logger.Information(s);
                        if (s.StartsWith("Listening on port: "))
                        {
                            serviceListenPort = Int32.Parse(Regex.Match(s, @"\d+").Value);
                            logger.Information("External halibut binary listening port is: " + serviceListenPort);
                        }

                        if (s.StartsWith("Polling listener is listening on port: "))
                        {
                            proxyClientListenPort = Int32.Parse(Regex.Match(s, @"\d+").Value);
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
                    logger.Error(e, "Error waiting for external process to start");
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

            logger.Information("External halibut binary started.");
            return (runningTentacle, serviceListenPort, proxyClientListenPort);
        }

        public class RoundTripRunningOldHalibutBinary : IDisposable
        {
            readonly CancellationTokenSource cts;
            readonly Task runningOldHalibutTask;
            readonly TmpDirectory tmpDirectory;
            public readonly int? serviceListenPort;
            public readonly int? proxyClientListenPort;

            public RoundTripRunningOldHalibutBinary(CancellationTokenSource cts, Task runningOldHalibutTask, TmpDirectory tmpDirectory, int? serviceListenPort, int? proxyClientListenPort)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
                this.serviceListenPort = serviceListenPort;
                this.proxyClientListenPort = proxyClientListenPort;
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