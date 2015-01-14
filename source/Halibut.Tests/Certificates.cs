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
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests
{
    public class Certificates
    {
        public static X509Certificate2 TentacleListening;
        public static string TentacleListeningPublicThumbprint;
        public static X509Certificate2 Octopus;
        public static string OctopusPublicThumbprint;
        public static X509Certificate2 TentaclePolling;
        public static string TentaclePollingPublicThumbprint;

        static Certificates()
        {
            TentacleListening = new X509Certificate2("Certificates\\TentacleListening.pfx");
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;
            Octopus = new X509Certificate2("Certificates\\Octopus.pfx");
            OctopusPublicThumbprint = Octopus.Thumbprint;
            TentaclePolling = new X509Certificate2("Certificates\\TentaclePolling.pfx");
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
        }
    }
}