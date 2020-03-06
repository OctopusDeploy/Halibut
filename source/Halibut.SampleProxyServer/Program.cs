using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.ServiceModel;
using Serilog;

namespace Halibut.SampleProxyServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            Console.Title = "Halibut Proxy";
            var certificate = new X509Certificate2("hproxy.pfx");
            var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8434);
            var services = new DelegateServiceFactory();
            using (var server = new HalibutRuntime(services, certificate))
            {
                server.Listen(endPoint);
                server.Trust("E27E2A6150E74959244DE91824172C84868FBF6E");
                
                server.Poll(new Uri("poll://SQ-PROXY"), new ServiceEndPoint(new Uri("https://localhost:8433"), "EDABCA3A77B9119B7ED1E0362F81C6F61C01F6C9"));
                
                Console.WriteLine("Server listening on port 8434. Type 'exit' to quit, or 'cls' to clear...");
                while (true)
                {
                    var line = Console.ReadLine();
                    if (string.Equals("cls", line, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.Clear();
                        continue;
                    }

                    if (string.Equals("q", line, StringComparison.OrdinalIgnoreCase))
                        return;

                    if (string.Equals("exit", line, StringComparison.OrdinalIgnoreCase))
                        return;

                    Console.WriteLine("Unknown command. Enter 'q' to quit.");
                }
            }
        }
    }
}