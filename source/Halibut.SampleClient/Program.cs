using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.SampleContracts;
using System.Collections.Generic;
using System.Net.Security;

namespace Halibut.SampleClient
{
    class Program
    {
        static bool MyCustomValidationFunction(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }

        public static void Main(string[] args)
        {
            Console.Title = "Halibut Client";

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";

            var certificate = new X509Certificate2("HalibutClient.pfx");

            using (var runtime = new HalibutRuntime(certificate))
            {
#if false
                // single endpoint
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
#endif

#if false
                // multiple endpoints possible at the same url
                List<string> trustedThumbprints = new List<string>();

                trustedThumbprints.Add("AF3A7A69AFE0D13130370B44A228F5CD15C069BC"); // not nserver thumbpring
                trustedThumbprints.Add("EF3A7A69AFE0D13130370B44A228F5CD15C069BC"); // server thumbpring
                trustedThumbprints.Add("BF3A7A69AFE0D13130370B44A228F5CD15C069BC"); // not server thumbpring

                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", trustedThumbprints);
#endif

#if true
                // custom validation
                Halibut.Transport.ClientCertificateValidator.customValidator = MyCustomValidationFunction;
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "");
#endif

                var result = calculator.Add(12, 18);

                Console.WriteLine("12 + 18 = " + result);
            }
        }
    }
}