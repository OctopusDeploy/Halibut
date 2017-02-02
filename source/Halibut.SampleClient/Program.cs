using System;
using System.Linq;
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
            var certificate = new X509Certificate2("HalibutClient.pfx");

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";
            using (var runtime = new HalibutRuntime(certificate))
            {
                //Begin make request of Listening server
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
                //End make request of Listening server

                //Begin make request of Polling server
                //var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8433);
                //runtime.Listen(endPoint);
                //runtime.Trust("EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
                //var calculator = runtime.CreateClient<ICalculatorService>("poll://SQ-TENTAPOLL", "2074529C99D93D5955FEECA859AEAC6092741205");
                //End make request of Polling server

                while (true)
                {
                    var result = calculator.Add(12, 18);

                    Console.WriteLine("12 + 18 = " + result);
                    Console.ReadKey();
                }
            }
        }
    }
}