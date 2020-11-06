using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Microsoft.VisualBasic;
using Serilog;

namespace Halibut.DebugUnsupComp
{
    class Program
    {
        static readonly X509Certificate2 ClientCertificate = new X509Certificate2("HalibutClient.pfx");
        static readonly X509Certificate2 ServerCertificate = new X509Certificate2("HalibutServer.pfx");
        const string PollUrl = "poll://SQ-TENTAPOLL";

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            //TestBaseline();
            //TestMismatchedContracts();
            //TestParallel();
            //TestParallelSlowWithJitter();
            //TestParallelClients();
            //TestSleep();
            //TestQueueing();
            //TestException();
            //TestInfiniteRecursion();
            TestLongStrings();
        }

        /// <summary>
        /// Baseline behaviour. If halibut is working properly, this should work.
        /// </summary>
        static void TestBaseline()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);

                for (var i = 0; i < 1000; i++)
                {
                    var result = calculator.Add(12, 18);
                    Debug.Assert(result == 30);
                }
            }
        }
        
        /// <summary>
        /// Tests two contracts with mismatched return types
        /// </summary>
        static void TestMismatchedContracts()
        {
            var clientServices = new DelegateServiceFactory();
            var serverServices = new DelegateServiceFactory();

            clientServices.Register<ICalculatorService>(() => new CalculatorService());
            serverServices.Register<Halibut.DebugUnsupComp.DiffNamespaceSoWeCanHaveDuplicateNames.ICalculatorService>(() => new Halibut.DebugUnsupComp.DiffNamespaceSoWeCanHaveDuplicateNames.CalculatorService2());

            using (var client = new HalibutRuntime(clientServices, ClientCertificate))
            using (var server = new HalibutRuntime(serverServices, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);

                for (var i = 0; i < 1000; i++)
                {
                    var result = calculator.Add(12, 18);
                    //Debug.Assert(result == 30);
                }
            }
        }

        static void TestParallel()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                
                Parallel.For(0, 1000, new ParallelOptions{ MaxDegreeOfParallelism = 1000 }, (i, state) =>
                {
                    var result = calculator.Add(12, 18);
                    Debug.Assert(result == 30);
                    Console.Write(".");
                });
            }
        }

        static void TestParallelSlowWithJitter()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                
                Parallel.For(0, 1000, new ParallelOptions{ MaxDegreeOfParallelism = 1000 }, (i, state) =>
                {
                    var result = calculator.SlowWithJitter(i);
                });
            }
        }
        
        static void TestParallelClients()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                Parallel.For(0, 1000, new ParallelOptions{ MaxDegreeOfParallelism = 1000 }, (i, state) =>
                {
                    var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                    var result = calculator.SlowWithJitter(i);
                });
            }
        }
        
        static void TestSleep()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                Parallel.For(0, 1000, new ParallelOptions{ MaxDegreeOfParallelism = 1000 }, (i, state) =>
                {
                    var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                    var result = calculator.ReallySlow();
                });
            }
        }
        
        /// <summary>
        /// Testing what happens when there's no server at the other end.
        /// </summary>
        static void TestQueueing()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            {
                client.Trust(ServerCertificate.Thumbprint);
                client.Listen();

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                calculator.ReallySlow();
            }
        }
        
        static void TestException()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);

                var result = calculator.ThisShouldThrow();
            }
        }

        /// <summary>
        /// Trying to test something that we can't recover from.
        /// </summary>
        static void TestInfiniteRecursion()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);

                var result = calculator.InfiniteRecursion();
            }
        }

        /// <summary>
        /// Test a long string that can be truncated, resulting in an invalid stream.
        /// </summary>
        static void TestLongStrings()
        {
            var services = new DelegateServiceFactory();

            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(services, ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);

                var result = calculator.SendAndReceiveString(new string('a', 1));
            }
        }
    }
}