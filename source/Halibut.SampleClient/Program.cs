using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Serilog;

namespace Halibut.SampleClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            Console.Title = "Halibut Client";
            var clientCertificate = new X509Certificate2("HalibutClient.pfx");
            var serverCertificate = new X509Certificate2("HalibutServer.pfx");
            
            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";
            using (var runtime = new HalibutRuntime(clientCertificate))
            {
                ICalculatorService calculatorClient;
                
                calculatorClient = MakeRequestOfListeningServer(runtime, hostName, port, serverCertificate);
                
                //calculatorClient = MakeRequestOfPollingServer(runtime, serverCertificate, clientCertificate);
                
                //calculatorClient = MakeRequestOfWebSocketPollingServer(runtime, serverCertificate, clientCertificate);

                while (true)
                {
                    try
                    {
                        var result = calculatorClient.Add(12, 18);
                        Console.WriteLine("12 + 18 = " + result);
                        Console.ReadKey();
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
                }
            }
        }

        static ICalculatorService MakeRequestOfWebSocketPollingServer(HalibutRuntime runtime, X509Certificate2 serverCertificate, X509Certificate2 clientCertificate)
        {
            try
            {
                AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:8433");
            }
            catch (CryptographicException cex)
            {
                if (cex.InnerException is PlatformNotSupportedException pex)
                    Log.Warning($"Unable to write to the local machine certificate store: {pex.Message}");
            }

            runtime.ListenWebSocket("https://+:8433/Halibut");
            runtime.Trust(serverCertificate.Thumbprint);
            return runtime.CreateClient<ICalculatorService>("poll://SQ-TENTAPOLL", clientCertificate.Thumbprint);
        }

        static ICalculatorService MakeRequestOfPollingServer(HalibutRuntime runtime, X509Certificate2 serverCertificate, X509Certificate2 clientCertificate)
        {
            var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8433);
            runtime.Listen(endPoint);
            runtime.Trust(serverCertificate.Thumbprint);
            return runtime.CreateClient<ICalculatorService>("poll://SQ-TENTAPOLL", clientCertificate.Thumbprint);
        }

        static ICalculatorService MakeRequestOfListeningServer(HalibutRuntime runtime, string hostName, string port, X509Certificate2 serverCertificate)
        {
            return runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", serverCertificate.Thumbprint);
        }

        static void AddSslCertToLocalStoreAndRegisterFor(string address)
        {
            var certificate = new X509Certificate2("HalibutSslCertificate.pfx", "password");
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();


            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo("netsh", $"http add sslcert ipport={address} certhash={certificate.Thumbprint} appid={{2e282bfb-fce9-40fc-a594-2136043e1c8f}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            proc.WaitForExit();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0 && !output.Contains("Cannot create a file when that file already exists"))
            {
                Console.WriteLine(output);
                Console.WriteLine(proc.StandardError.ReadToEnd());
                throw new Exception("Could not bind cert to port");
            }
        }
    }
}