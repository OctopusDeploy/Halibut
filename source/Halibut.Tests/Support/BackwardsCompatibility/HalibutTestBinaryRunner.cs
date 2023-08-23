using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Halibut.Logging;
using Halibut.Tests.Support.ExtensionMethods;
using Nito.AsyncEx;
using NUnit.Framework;
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
        readonly LogLevel halibutLogLevel;
        readonly ILogger logger = new SerilogLoggerBuilder().Build().ForContext<HalibutRuntimeBuilder>();
        readonly Uri webSocketServiceEndpointUri;
        readonly OldServiceAvailableServices availableServices;

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string? version, ProxyDetails? proxyDetails, LogLevel halibutLogLevel, OldServiceAvailableServices availableServices)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
            this.proxyDetails = proxyDetails;
            this.halibutLogLevel = halibutLogLevel;
            this.availableServices = availableServices;
        }
        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, int? clientServicePort, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string? version, ProxyDetails proxyDetails, LogLevel halibutLoggingLevel, OldServiceAvailableServices availableServices) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails, halibutLoggingLevel, availableServices)
        {
            this.clientServicePort = clientServicePort;
        }

        public HalibutTestBinaryRunner(ServiceConnectionType serviceConnectionType, Uri webSocketServiceEndpointUri, CertAndThumbprint clientCertAndThumbprint, CertAndThumbprint serviceCertAndThumbprint, string? version, ProxyDetails? proxyDetails, LogLevel halibutLoggingLevel, OldServiceAvailableServices availableServices) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails, halibutLoggingLevel, availableServices)
        {
            this.webSocketServiceEndpointUri = webSocketServiceEndpointUri;
        }

        public async Task<RunningOldHalibutBinary> Run()
        {
            var compatBinaryStayAlive = new CompatBinaryStayAlive(); 
            var settings = new Dictionary<string, string>
            {
                { "mode", "serviceonly" },
                { "tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath },
                { "octopusthumbprint", clientCertAndThumbprint.Thumbprint },
                { "halibutloglevel", halibutLogLevel.ToString() },
                { CompatBinaryStayAlive.StayAliveFilePathEnvVarKey, compatBinaryStayAlive.LockFile },
                { "WithStandardServices", availableServices.HasStandardServices.ToString() },
                { "WithCachingService", availableServices.HasCachingService.ToString() },
                { "WithTentacleServices", availableServices.HasTentacleServices.ToString() },
                { "TestTimeout", TestContext.CurrentContext.GetTestTimeout()?.ToString() ?? string.Empty }
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

                return new RunningOldHalibutBinary(cts, task, tmp, serviceListenPort, compatBinaryStayAlive);
            }
            catch (Exception)
            {
                cts.Cancel();
                throw;
            }
        }

        async Task<(Task, int?)> StartHalibutTestBinary(string version, Dictionary<string, string> settings, TmpDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new AsyncManualResetEvent();
            hasTentacleStarted.Reset();

            int? serviceListenPort = null;
            var runningTentacle = Task.Run(async () =>
            {
                try
                {
                    async Task ProcessLogs(string s, CancellationToken ct)
                    {
                        await Task.CompletedTask;
                        logger.Information(s);
                        if (s.StartsWith("Listening on port: "))
                        {
                            serviceListenPort = int.Parse(Regex.Match(s, @"\d+").Value);
                        }

                        if (s.Contains("RunningAndReady")) hasTentacleStarted.Set();
                    }

                    await Cli.Wrap(new HalibutTestBinaryPath().BinPath(version))
                        .WithArguments(new string[0])
                        .WithWorkingDirectory(tmp.FullPath)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(ProcessLogs))
                        .WithEnvironmentVariables(settings)
                        .ExecuteAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Don't throw when we cancel the running of the binary, this is an expected way of killing it.
                }
                catch (Exception e)
                {
                    logger.Information(e, "Error running Halibut Test Binary");
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, hasTentacleStarted.WaitAsync(cancellationToken), Task.Delay(TimeSpan.FromMinutes(1), cancellationToken));

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

            readonly CompatBinaryStayAlive compatBinaryStayAlive;

            public RunningOldHalibutBinary(CancellationTokenSource cts, Task runningOldHalibutTask, TmpDirectory tmpDirectory, int? serviceListenPort, CompatBinaryStayAlive compatBinaryStayAlive)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
                this.ServiceListenPort = serviceListenPort;
                this.compatBinaryStayAlive = compatBinaryStayAlive;
            }

            public void Dispose()
            {
                cts.Cancel();
                compatBinaryStayAlive.Dispose();
                tmpDirectory.Dispose();
            }
        }
    }
}