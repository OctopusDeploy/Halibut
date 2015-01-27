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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;

namespace Halibut.SampleClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Halibut Client";

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";

            var certificate = new X509Certificate2("HalibutClient.pfx");

            using (var runtime = new HalibutRuntime(certificate))
            {
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");

                var result = calculator.Add(12, 18);

                Console.WriteLine("12 + 18 = " + result);
            }
        }
    }
}