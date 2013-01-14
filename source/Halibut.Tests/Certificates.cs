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
        public static X509Certificate2 Alice;
        public static string AlicePublicThumbprint;
        public static X509Certificate2 Bob;
        public static string BobPublicThumbprint;
        public static X509Certificate2 Eve;
        public static string EvePublicThumbprint;

        static Certificates()
        {
            Alice = new X509Certificate2("Certificates\\HalibutAlice.pfx");
            AlicePublicThumbprint = Alice.Thumbprint;
            Bob = new X509Certificate2("Certificates\\HalibutBob.pfx");
            BobPublicThumbprint = Bob.Thumbprint;
            Eve = new X509Certificate2("Certificates\\HalibutEve.pfx");
            EvePublicThumbprint = Eve.Thumbprint;
        }
    }
}