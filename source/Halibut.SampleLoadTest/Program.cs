using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.SampleLoadTest
{
    class Program
    {
        static X509Certificate2 ClientCertificate = new X509Certificate2("HalibutClient.pfx");
        static X509Certificate2 ServerCertificate = new X509Certificate2("HalibutServer.pfx");

        const int Servers = 100;
        const int ClientsPerServer = 1;
        const int RequestsPerClient = 100;

        static void Main(string[] args)
        {
            Console.Title = "Halibut Load Test";

            var servers = new List<int>();
            for (int i = 0; i < Servers; i++)
            {
                servers.Add(RunServer());
            }

            var tasks = new List<Action>();
            foreach (var port in servers)
            {
                for (int i = 0; i < ClientsPerServer; i++)
                {
                    tasks.Add(() =>
                    {
                        RunClient(port);
                    });
                }
            }

            var watch = Stopwatch.StartNew();
            Parallel.ForEach(tasks, t => t());
            Console.WriteLine("Done in: {0:n0}ms", watch.ElapsedMilliseconds);
        }

        static int RunServer()
        {
            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new CalculatorService());

            var server = new HalibutRuntime(services, ServerCertificate);
            server.Trust("2074529C99D93D5955FEECA859AEAC6092741205");
            var port = server.Listen();
            return port;
        }

        static void RunClient(int port)
        {
            using (var runtime = new HalibutRuntime(ClientCertificate))
            {
                var calculator = runtime.CreateClient<ICalculatorService>("https://localhost:" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");

                for (int i = 0; i < RequestsPerClient; i++)
                {
                    var result = calculator.Add(12, 18);
                    Debug.Assert(result == 30);
                }
            }
        }
    }
}
