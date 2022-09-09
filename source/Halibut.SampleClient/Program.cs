using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
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
                .MinimumLevel.Verbose()
                .CreateLogger();

            Console.Title = "Halibut Client";
            var certificate = new X509Certificate2("HalibutClient.pfx");

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";
            using (var runtime = new HalibutRuntime(certificate))
            {
                //Begin make request of Listening server
                //var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
                //End make request of Listening server

                //Begin make request of Polling server
                //var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8433);
                //runtime.Listen(endPoint);
                //runtime.Trust("EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
                //var calculator = runtime.CreateClient<ICalculatorService>("poll://SQ-TENTAPOLL", "2074529C99D93D5955FEECA859AEAC6092741205");
                //End make request of Polling server

                //Begin make request of WebSocket Polling server
                AddSslCertToLocalStoreAndRegisterFor("0.0.0.0:8433");
                runtime.ListenWebSocket("https://+:8433/Halibut");
                runtime.Trust("EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
                var calculator = runtime.CreateClient<ICalculatorService>("poll://SQ-TENTAPOLL", "2074529C99D93D5955FEECA859AEAC6092741205");
                //End make request of WebSocket Polling server

                while (true)
                {
                    try
                    {
                        var result = calculator.Add(12, 18);
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
