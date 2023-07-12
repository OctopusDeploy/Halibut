using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        readonly ProxyDetails? proxyDetails;
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<HalibutRuntimeBuilder>();
        readonly Uri webSocketServiceEndpointUri;

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version, ProxyDetails? proxyDetails)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
            this.proxyDetails = proxyDetails;
        }
        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, int? clientServicePort, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version, ProxyDetails? proxyDetails) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails)
        {
            this.clientServicePort = clientServicePort;
        }

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, Uri webSocketServiceEndpointUri, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string version, ProxyDetails? proxyDetails) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails)
        {
            this.webSocketServiceEndpointUri = webSocketServiceEndpointUri;
        }

        public async Task<RunningOldHalibutBinary> Run()
        {
            var settings = new Dictionary<string, string>
            {
                { "mode", "serviceonly" },
                { "tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath },
                { "octopusthumbprint", clientCertAndThumbprint.Thumbprint }
            };

            if (proxyDetails != null)
            {
                settings.Add("proxydetails_host", proxyDetails.Host);
                settings.Add("proxydetails_password", proxyDetails.Password);
                settings.Add("proxydetails_username", proxyDetails.UserName);
                settings.Add("proxydetails_port", proxyDetails.Port.ToString());
                settings.Add("proxydetails_type", proxyDetails.Type.ToString());
            }

            if (clientServicePort != null)
            {
                settings.Add("octopusservercommsport", "https://localhost:" + clientServicePort);
            }
            else if (webSocketServiceEndpointUri != null)
            {
                settings.Add("sslthubprint", Certificates.SslThumbprint);
                settings.Add("octopusservercommsport", webSocketServiceEndpointUri.ToString());
            }

            settings.Add("ServiceConnectionType", serviceConnectionType.ToString());

            var cts = new CancellationTokenSource();

            try
            {
                var tmp = new TmpDirectory();

                var (task, serviceListenPort) = await StartHalibutTestBinary(version, settings, tmp, cts.Token);

                return new RunningOldHalibutBinary(cts, task, tmp, serviceListenPort);
            }
            catch (Exception)
            {
                cts.Cancel();
                throw;
            }
        }

        async Task<(Task, int?)> StartHalibutTestBinary(string version, Dictionary<string, string> settings, TmpDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            int? serviceListenPort = null;
            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    void ProcessLogs(string s)
                    {
                        logger.Information(s);
                        if (s.StartsWith("Listening on port: "))
                        {
                            serviceListenPort = int.Parse(Regex.Match(s, @"\d+").Value);
                        }

                        if (s.Contains("RunningAndReady")) hasTentacleStarted.Set();
                    }

                    ShellExecutor.ExecuteCommand(new HalibutTestBinaryPath().BinPath(version),
                        "",
                        tmp.FullPath,
                        ProcessLogs,
                        ProcessLogs,
                        ProcessLogs,
                        customEnvironmentVariables: settings,
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
            public int? ServiceListenPort { get; }

            public RunningOldHalibutBinary(CancellationTokenSource cts, Task runningOldHalibutTask, TmpDirectory tmpDirectory, int? serviceListenPort)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
                this.ServiceListenPort = serviceListenPort;
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