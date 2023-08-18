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
            using var cancellationTokenSource = new CancellationTokenSource();
            Task actionTask;

            var mode = SettingsHelper.GetSetting("mode") ?? string.Empty;
            Console.WriteLine($"Mode is: {mode}");

            if (mode.Equals("serviceonly"))
            {
                actionTask = RunExternalService(cancellationTokenSource.Token);
            }
            else if(mode.Equals("proxy"))
            {
                actionTask = ProxyServiceForwardingRequestToClient.Run(cancellationTokenSource.Token);
            }
            else
            {
                Console.WriteLine($"Unknown mode: {mode}");
                throw new Exception($"Unknown mode: {mode}");
            }

            var timeoutTask = GetTestTimeoutTask(cancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(actionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Console.WriteLine($"Program will exit as the TestTimeout of {SettingsHelper.GetTestTimeout()} has elapsed");
                cancellationTokenSource.Cancel();
                return -10060;
            }

            return 1;
        }

        static async Task RunExternalService(CancellationToken cancellationToken)
        {
            var serviceCert = SettingsHelper.GetServiceCertificate();
            var serviceConnectionType = SettingsHelper.GetServiceConnectionType();
            var octopusThumbprint = SettingsHelper.GetClientThumbprint();

            string addressToPoll = null;

            if (serviceConnectionType is ServiceConnectionType.Polling or ServiceConnectionType.PollingOverWebSocket)
            {
                addressToPoll = SettingsHelper.GetSetting("octopusservercommsport");
                Console.WriteLine($"Will poll: {addressToPoll}");
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
                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll!), octopusThumbprint, proxyDetails));
                        break;
                    case ServiceConnectionType.PollingOverWebSocket:
                        var sslThubprint = SettingsHelper.GetSetting("sslthubprint");
                        Console.WriteLine($"Using SSL thumbprint: {sslThubprint}");

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll!), sslThubprint, proxyDetails));
                        break;
                    case ServiceConnectionType.Listening:
                        var port = tentaclePolling.Listen();
                        Console.WriteLine($"Listening on port: {port}");
                        tentaclePolling.Trust(octopusThumbprint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Console.WriteLine("RunningAndReady");
                Console.WriteLine("Will Now sleep");
                await Console.Out.FlushAsync();

                // Run until the Program is terminated
                await StayAliveUntilHelper.WaitUntilSignaledToDie(cancellationToken);
            }
        }

        static async Task GetTestTimeoutTask(CancellationToken cancellationToken)
        {
            var timeout = SettingsHelper.GetTestTimeout();

            await Task.Delay(timeout,  cancellationToken);
        }
    }
}