using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;
using Halibut.TestUtils.SampleProgram.Base.Services;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class BackwardsCompatProgramBase
    {
        public static async Task<int> Main()
        {
            using var cancellationTokenSource = new CancellationTokenSource(SettingsHelper.GetTestTimeout());
            using var registration = cancellationTokenSource.Token.Register(() =>
            {
                Environment.Exit(-10060);
            });

            var mode = SettingsHelper.GetSetting("mode") ?? string.Empty;
            NonBlockingConsole.WriteLine($"Mode is: {mode}");

            if (mode.Equals("serviceonly"))
            {
                await RunExternalService(cancellationTokenSource.Token);
            }
            else if(mode.Equals("proxy"))
            {
                await ProxyServiceForwardingRequestToClient.Run(cancellationTokenSource.Token);
            }
            else
            {
                NonBlockingConsole.WriteLine($"Unknown mode: {mode}");
                NonBlockingConsole.FlushBeforeExit();
                throw new Exception($"Unknown mode: {mode}");
            }

            return 1;
        }

        static async Task RunExternalService(CancellationToken cancellationToken)
        {
            var serviceCert = SettingsHelper.GetServiceCertificate();
            var serviceConnectionType = SettingsHelper.GetServiceConnectionType();
            var octopusThumbprint = SettingsHelper.GetClientThumbprint();

            var addressToPoll = string.Empty;

            if (serviceConnectionType is ServiceConnectionType.Polling or ServiceConnectionType.PollingOverWebSocket)
            {
                addressToPoll = SettingsHelper.GetSetting("octopusservercommsport");
                NonBlockingConsole.WriteLine($"Will poll: {addressToPoll}");
            }

            var proxyDetails = SettingsHelper.GetProxyDetails();
            var services = ServiceFactoryFactory.CreateServiceFactory();

            using (var tentaclePolling = new HalibutRuntimeBuilder()
                       .WithServiceFactory(services)
                       .WithServerCertificate(serviceCert)
                       .WithLogFactory(new TestContextLogFactory("ExternalService", SettingsHelper.GetHalibutLogLevel()))
                       .Build())
            {
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint, proxyDetails));
                        break;
                    case ServiceConnectionType.PollingOverWebSocket:
                        FixHungWebSocketsHack.EnableHack();
                        var sslThubprint = SettingsHelper.GetSetting("sslthubprint");
                        NonBlockingConsole.WriteLine($"Using SSL thumbprint: {sslThubprint}");

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), sslThubprint, proxyDetails));
                        break;
                    case ServiceConnectionType.Listening:
                        var port = tentaclePolling.Listen();
                        NonBlockingConsole.WriteLine($"Listening on port: {port}");
                        tentaclePolling.Trust(octopusThumbprint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                NonBlockingConsole.WriteLine("RunningAndReady");
                NonBlockingConsole.WriteLine("Will Now sleep");
                await NonBlockingConsole.FlushAsync();

                // Run until the Program is terminated
                await StayAliveUntilHelper.WaitUntilSignaledToDie();
            }
        }
    }
}