using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Serilog;

namespace Halibut.SampleClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            Console.Title = "Halibut Client";

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";

            var certificate = new X509Certificate2("HalibutClient.pfx");

            using (var runtime = new HalibutRuntime(certificate))
            {
                Console.WriteLine("creating calculator");
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");

                Console.WriteLine("making call 1");
                var result = calculator.Add(12, 18);
                Console.WriteLine("making call 2");
                result = calculator.Add(12, 18);


                Console.WriteLine("12 + 18 = " + result);
                Console.ReadKey();
            }
        }
    }
}