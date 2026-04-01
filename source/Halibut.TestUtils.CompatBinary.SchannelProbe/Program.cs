using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.SchannelProbe
{
    public class Program
    {
        public static async Task<int> Main()
        {
            using var cts = new CancellationTokenSource(GetTestTimeout());
            using var _ = cts.Token.Register(() => Environment.Exit(-10060));

            var mode = GetSetting("mode");
            Console.WriteLine($"Mode is: {mode}");

            if (mode.Equals("serviceonly", StringComparison.OrdinalIgnoreCase))
            {
                await RunExternalService(cts.Token);
            }
            else
            {
                Console.WriteLine($"Unknown mode: {mode}");
                throw new Exception($"Unknown mode: {mode}");
            }

            return 1;
        }

        static async Task RunExternalService(CancellationToken cancellationToken)
        {
            var serviceCert = new X509Certificate2(GetSetting("tentaclecertpath"));
            var octopusThumbprint = GetSetting("octopusthumbprint");
            var serviceConnectionType = ParseServiceConnectionType(GetSetting("ServiceConnectionType"));

            var services = new DelegateServiceFactory();
            services.Register<ISayHelloService, IAsyncSayHelloService>(() => new SayHelloServiceImpl());

            using var tentacle = new HalibutRuntimeBuilder()
                .WithServiceFactory(services)
                .WithServerCertificate(serviceCert)
                .WithLogFactory(new LogFactory())
                .Build();

            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    var addressToPoll = GetSetting("octopusservercommsport");
                    tentacle.Poll(
                        new Uri("poll://SQ-TENTAPOLL"),
                        new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint, null, new HalibutTimeoutsAndLimits()),
                        cancellationToken);
                    break;
                case ServiceConnectionType.Listening:
                    var port = tentacle.Listen();
                    Console.WriteLine($"Listening on port: {port}");
                    tentacle.Trust(octopusThumbprint);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceConnectionType));
            }

            Console.WriteLine("RunningAndReady");
            await Console.Out.FlushAsync();
            await WaitUntilSignaledToDie();
        }

        static async Task WaitUntilSignaledToDie()
        {
            var stayAliveFile = GetSetting("CompatBinaryStayAliveFilePath");
            while (true)
            {
                try
                {
                    using (new FileStream(stayAliveFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }

                    try
                    {
                        File.Delete(stayAliveFile);
                    }
                    finally
                    {
                        Environment.Exit(0);
                    }
                }
                catch (Exception)
                {
                }

                if (!File.Exists(stayAliveFile))
                {
                    Environment.Exit(0);
                }

                await Task.Delay(2000);
            }
        }

        static ServiceConnectionType ParseServiceConnectionType(string s)
        {
            if (Enum.TryParse(s, out ServiceConnectionType result))
                return result;
            throw new Exception($"Unknown service connection type '{s}'");
        }

        static TimeSpan GetTestTimeout()
        {
            var timeoutString = GetSetting("TestTimeout");
            return string.IsNullOrWhiteSpace(timeoutString) ? TimeSpan.FromMinutes(15) : TimeSpan.Parse(timeoutString);
        }

        static string GetSetting(string name) => Environment.GetEnvironmentVariable(name) ?? string.Empty;
    }

    enum ServiceConnectionType
    {
        Polling,
        Listening
    }

    class SayHelloServiceImpl : IAsyncSayHelloService
    {
        public async Task<string> SayHelloAsync(string name, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return name + "...";
        }
    }
}
