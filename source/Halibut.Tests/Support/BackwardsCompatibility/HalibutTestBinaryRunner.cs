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
        readonly string? version;
        readonly ProxyDetails? proxyDetails;
        readonly LogLevel halibutLogLevel;
        readonly ILogger logger;
        readonly Uri? webSocketServiceEndpointUri;
        readonly OldServiceAvailableServices availableServices;

        public HalibutTestBinaryRunner(
            ServiceConnectionType serviceConnectionType, 
            CertAndThumbprint clientCertAndThumbprint, 
            CertAndThumbprint serviceCertAndThumbprint, 
            string? version, 
            ProxyDetails? proxyDetails, 
            LogLevel halibutLogLevel, 
            OldServiceAvailableServices availableServices,
            ILogger logger)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
            this.proxyDetails = proxyDetails;
            this.halibutLogLevel = halibutLogLevel;
            this.availableServices = availableServices;
            this.logger = logger.ForContext<HalibutRuntimeBuilder>();
        }
        public HalibutTestBinaryRunner(
            ServiceConnectionType serviceConnectionType, 
            int? clientServicePort, 
            CertAndThumbprint clientCertAndThumbprint, 
            CertAndThumbprint serviceCertAndThumbprint, 
            string? version, 
            ProxyDetails proxyDetails, 
            LogLevel halibutLoggingLevel, 
            OldServiceAvailableServices availableServices,
            ILogger logger) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails, halibutLoggingLevel, availableServices, logger)
        {
            this.clientServicePort = clientServicePort;
        }

        public HalibutTestBinaryRunner(
            ServiceConnectionType serviceConnectionType,
            Uri webSocketServiceEndpointUri, 
            CertAndThumbprint clientCertAndThumbprint, 
            CertAndThumbprint serviceCertAndThumbprint, 
            string? version, 
            ProxyDetails? proxyDetails,
            LogLevel halibutLoggingLevel, 
            OldServiceAvailableServices availableServices,
            ILogger logger) :
            this(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint, version, proxyDetails, halibutLoggingLevel, availableServices, logger)
        {
            this.webSocketServiceEndpointUri = webSocketServiceEndpointUri;
        }

        public async Task<RunningOldHalibutBinary> Run()
        {
            var compatBinaryStayAlive = new CompatBinaryStayAlive(logger); 
            var settings = new Dictionary<string, string?>
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

            if (proxyDetails is not null)
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
            
            var tmpDirectory = new TmpDirectory();

            var (task, serviceListenPort, runningTentacleCancellationTokenSource) = await StartHalibutTestBinary(version, settings, tmpDirectory);

            return new RunningOldHalibutBinary(runningTentacleCancellationTokenSource, task, tmpDirectory, serviceListenPort, compatBinaryStayAlive);
        }

        async Task<(Task RunningTentacleTask, int? ServiceListenPort, CancellationTokenSource RunningTentacleCancellationTokenSource)> StartHalibutTestBinary(
            string? version, 
            Dictionary<string, string?> settings, 
            TmpDirectory tmp)
        {
            var hasTentacleStarted = new AsyncManualResetEvent();
            hasTentacleStarted.Reset();

            int? serviceListenPort = null;

            var runningTentacleCancellationTokenSource = new CancellationTokenSource();

            try
            {

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

                        await Cli.Wrap(new HalibutTestBinaryPath().BinPath(version!))
                            .WithArguments(new string[0])
                            .WithWorkingDirectory(tmp.FullPath)
                            .WithStandardOutputPipe(PipeTarget.ToDelegate(ProcessLogs))
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(ProcessLogs))
                            .WithEnvironmentVariables(settings)
                            .ExecuteAsync(runningTentacleCancellationTokenSource.Token);
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
                }, runningTentacleCancellationTokenSource.Token);

                using var whenAnyCleanupCancellationTokenSource = new CancellationTokenSource();
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(runningTentacleCancellationTokenSource.Token, whenAnyCleanupCancellationTokenSource.Token);

                var completedTask = await Task.WhenAny(runningTentacle, hasTentacleStarted.WaitAsync(), Task.Delay(TimeSpan.FromSeconds(30), linkedCancellationTokenSource.Token));
                
                if (completedTask == runningTentacle)
                {
#pragma warning disable VSTHRD103
                    whenAnyCleanupCancellationTokenSource.Cancel();
                    runningTentacleCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                    // Will throw the startup exception.
                    await runningTentacle;
                }

                if (!hasTentacleStarted.IsSet)
                {
#pragma warning disable VSTHRD103
                    whenAnyCleanupCancellationTokenSource.Cancel();
                    runningTentacleCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                    throw new Exception("Halibut test binary did not appear to start correctly");
                }

#pragma warning disable VSTHRD103
                whenAnyCleanupCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

                return (runningTentacle, serviceListenPort, runningTentacleCancellationTokenSource);
            }
            catch (Exception)
            {
#pragma warning disable VSTHRD103
                runningTentacleCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                runningTentacleCancellationTokenSource.Dispose();
                throw;
            }
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
                cts.Dispose();
                compatBinaryStayAlive.Dispose();
                tmpDirectory.Dispose();
            }
        }
    }
}