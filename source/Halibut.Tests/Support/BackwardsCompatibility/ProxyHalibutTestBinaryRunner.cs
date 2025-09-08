#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Halibut.Diagnostics;
using Nito.AsyncEx;
using Serilog;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class ProxyHalibutTestBinaryRunner
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly int? proxyClientListeningPort;
        readonly CertAndThumbprint clientCertAndThumbprint;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly string? version;
        readonly ProxyDetails? proxyDetails;
        readonly string? webSocketPath;
        readonly LogLevel halibutLogLevel;
        readonly ILogger logger;
        readonly Uri? realServiceListenAddress;

        public ProxyHalibutTestBinaryRunner(
            ServiceConnectionType serviceConnectionType,
            int? proxyClientListeningPort,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint,
            Uri? realServiceListenAddress,
            string? version,
            ProxyDetails? proxyDetails,
            string? webSocketPath,
            LogLevel halibutLogLevel,
            ILogger logger)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.proxyClientListeningPort = proxyClientListeningPort;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.version = version;
            this.proxyDetails = proxyDetails;
            this.halibutLogLevel = halibutLogLevel;
            this.logger = logger;
            this.webSocketPath = webSocketPath;
            this.realServiceListenAddress = realServiceListenAddress;
            this.logger = logger.ForContext<ProxyHalibutTestBinaryRunner>();
        }

        public async Task<RoundTripRunningOldHalibutBinary> Run()
        {
            var compatBinaryStayAlive = new CompatBinaryStayAlive(logger);
            var settings = new Dictionary<string, string?>
            {
                { "mode", "proxy" },
                { "tentaclecertpath", serviceCertAndThumbprint.CertificatePfxPath },
                { "octopuscertpath", clientCertAndThumbprint.CertificatePfxPath },
                { "websocketpath", webSocketPath ?? string.Empty },
                { "ServiceConnectionType", serviceConnectionType.ToString() },
                { "sslthubprint", Certificates.SslThumbprint },
                { "halibutloglevel", halibutLogLevel.ToString() },
                { CompatBinaryStayAlive.StayAliveFilePathEnvVarKey, compatBinaryStayAlive.LockFile }
            };

            if (proxyDetails is not null)
            {
                settings.Add("proxydetails_host", proxyDetails.Host);
                settings.Add("proxydetails_password", proxyDetails.Password);
                settings.Add("proxydetails_username", proxyDetails.UserName);
                settings.Add("proxydetails_port", proxyDetails.Port.ToString());
                settings.Add("proxydetails_type", proxyDetails.Type.ToString());
            }

            if (proxyClientListeningPort != null)
            {
                settings.Add("octopusservercommsport", "https://localhost:" + proxyClientListeningPort);
            }

            if (realServiceListenAddress != null)
            {
                settings.Add("realServiceListenAddress", realServiceListenAddress.ToString());
            }
            
            var tmpDirectory = new TmpDirectory();

            var (task, serviceListenPort, proxyClientListenPort, runningTentacleCancellationTokenSource) = await StartHalibutTestBinary(version, settings, tmpDirectory);

            return new RoundTripRunningOldHalibutBinary(runningTentacleCancellationTokenSource, task, tmpDirectory, serviceListenPort, proxyClientListenPort, compatBinaryStayAlive);
        }

        async Task<(Task RunningTentacleTask, int? ServiceListenPort, int? ProxyClientListenPort, CancellationTokenSource RunningTentacleCancellationTokenSource)> StartHalibutTestBinary(
            string? version, 
            Dictionary<string, string?> settings,
            TmpDirectory tmp)
        {
            var hasTentacleStarted = new AsyncManualResetEvent();
            hasTentacleStarted.Reset();

            int? serviceListenPort = null;
            int? proxyClientListenPort = null;

            var runningTentacleCancellationTokenSource = new CancellationTokenSource();

            try
            {
                var runningTentacle = Task.Run(async () =>
                {
                    try
                    {
                        await Cli.Wrap(new HalibutTestBinaryPath().BinPath(version!))
                            .WithArguments(Array.Empty<string>())
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
                        logger.Error(e, "Error waiting for external process to start");
                        throw;
                    }

                    async Task ProcessLogs(string s, CancellationToken ct)
                    {
                        await Task.CompletedTask;
                        logger.Information(s);
                        if (s.StartsWith("Listening on port: "))
                        {
                            serviceListenPort = int.Parse(Regex.Match(s, @"\d+").Value);
                            logger.Information("External halibut binary listening port is: " + serviceListenPort);
                        }

                        if (s.StartsWith("Polling listener is listening on port: "))
                        {
                            proxyClientListenPort = int.Parse(Regex.Match(s, @"\d+").Value);
                        }

                        if (s.Contains("RunningAndReady")) hasTentacleStarted.Set();
                    }
                }, runningTentacleCancellationTokenSource.Token);

                using var whenAnyCleanupCancellationTokenSource = new CancellationTokenSource();
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(runningTentacleCancellationTokenSource.Token, whenAnyCleanupCancellationTokenSource.Token);

                var completedTask = await Task.WhenAny(runningTentacle, hasTentacleStarted.WaitAsync(), Task.Delay(TimeSpan.FromSeconds(30), linkedCancellationTokenSource.Token));

                if (completedTask == runningTentacle)
                {
#if NET8_0_OR_GREATER
                    await whenAnyCleanupCancellationTokenSource.CancelAsync();
                    await runningTentacleCancellationTokenSource.CancelAsync();
#else
                    whenAnyCleanupCancellationTokenSource.Cancel();
                    runningTentacleCancellationTokenSource.Cancel();
#endif
                    // Will throw the startup exception.
                    await runningTentacle;
                }

                if (!hasTentacleStarted.IsSet)
                {
#if NET8_0_OR_GREATER
                    await whenAnyCleanupCancellationTokenSource.CancelAsync();
                    await runningTentacleCancellationTokenSource.CancelAsync();
#else
                    whenAnyCleanupCancellationTokenSource.Cancel();
                    runningTentacleCancellationTokenSource.Cancel();
#endif
                    throw new Exception("Halibut test binary did not appear to start correctly");
                }

#if NET8_0_OR_GREATER
                await whenAnyCleanupCancellationTokenSource.CancelAsync();
#else
                whenAnyCleanupCancellationTokenSource.Cancel();
#endif

                logger.Information("External halibut binary started.");
                return (runningTentacle, serviceListenPort, proxyClientListenPort, runningTentacleCancellationTokenSource);
            }
            catch (Exception)
            {
#if NET8_0_OR_GREATER
                await runningTentacleCancellationTokenSource.CancelAsync();
#else
                runningTentacleCancellationTokenSource.Cancel();
#endif
                runningTentacleCancellationTokenSource.Dispose();
                throw;
            }
        }

        public class RoundTripRunningOldHalibutBinary : IDisposable
        {
            readonly CancellationTokenSource cts;
            readonly Task runningOldHalibutTask;
            readonly TmpDirectory tmpDirectory;
            readonly CompatBinaryStayAlive compatBinaryStayAlive;

            public RoundTripRunningOldHalibutBinary(
                CancellationTokenSource cts,
                Task runningOldHalibutTask,
                TmpDirectory tmpDirectory,
                int? serviceListenPort,
                int? proxyClientListenPort,
                CompatBinaryStayAlive compatBinaryStayAlive)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
                ServiceListenPort = serviceListenPort;
                ProxyClientListenPort = proxyClientListenPort;
                this.compatBinaryStayAlive = compatBinaryStayAlive;
            }
            public int? ServiceListenPort { get; }
            public int? ProxyClientListenPort { get; }

            public void Dispose()
            {
                cts.Cancel();
                cts.Dispose();
                compatBinaryStayAlive.Dispose();
                tmpDirectory.Dispose();

                if (runningOldHalibutTask.IsCanceled || runningOldHalibutTask.IsCompleted || runningOldHalibutTask.IsFaulted)
                {
                    runningOldHalibutTask.Dispose();
                }
            }
        }
    }
}