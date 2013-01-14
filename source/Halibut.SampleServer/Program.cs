// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Halibut.Server;
using Halibut.Server.Security;

namespace Halibut.SampleServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Halibut Server";
            var certificate = new X509Certificate2("HalibutServer.pfx");

            var endPoint = new IPEndPoint(IPAddress.Any, 8433);

            var server = new HalibutServer(endPoint, certificate);
            server.Services.Register<ICalculatorService, CalculatorService>();
            server.Options.ClientCertificateValidator = ValidateClientCertificate;
            server.Start();

            Console.WriteLine("Server listening on port 8433...");

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

        static CertificateValidationResult ValidateClientCertificate(X509Certificate2 clientcertificate)
        {
            return clientcertificate.Thumbprint == "2074529C99D93D5955FEECA859AEAC6092741205"
                       ? CertificateValidationResult.Valid
                       : CertificateValidationResult.Rejected;
        }
    }
}