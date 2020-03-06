using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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
            var certificate = new X509Certificate2("itops.pfx");
            
            using (var runtime = new HalibutRuntime(certificate))
            {
                var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8433);
                runtime.Listen(endPoint);
                runtime.Route(
                    new ServiceEndPoint("poll://SQ-EXTENDER", "E27E2A6150E74959244DE91824172C84868FBF6E"), 
                    new ServiceEndPoint("poll://SQ-PROXY", "177B1A7194EC6C8A80F5666243E5021DF2DC03C2"));
                runtime.Trust("177B1A7194EC6C8A80F5666243E5021DF2DC03C2");
                
                var random = new Random();
                var calculator = runtime.CreateClient<ICalculatorService>("poll://SQ-EXTENDER", "E27E2A6150E74959244DE91824172C84868FBF6E");
                while (true)
                {
                    try
                    {
                        Thread.Sleep(2000);
                        var a = random.Next(0, 100);
                        var b = random.Next(0, 100);
                        Console.WriteLine($"Adding {a} + {b} ...");
                        var result = calculator.Add(a, b);
                        Console.WriteLine($"... = {result}");
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }
                }
            }
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