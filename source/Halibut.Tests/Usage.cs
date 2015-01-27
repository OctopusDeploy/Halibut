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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Halibut.Client;
using Halibut.Protocol;
using Halibut.Server;
using Halibut.Server.Dispatch;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class Usage
    {
        DelegateServiceFactory services;

        [SetUp]
        public void SetUp()
        {
            services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
        }

        [Test]
        public void OctopusCanSendMessagesToListeningTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 2000; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }

                Console.WriteLine("Complete in {0:n0}ms", watch.ElapsedMilliseconds);
            }
        }

        [Test]
        public void OctopusCanSendMessagesToPollingTentacle()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                for (var i = 0; i < 100; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }
            }
        }

        [Test]
        public void FailsWhenSendingToPollingMachineButNothingPicksItUp()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var error = Assert.Throws<HalibutClientException>(() => echo.SayHello("Paul"));
                Assert.That(error.Message, Is.StringContaining("the polling endpoint did not collect the request within the allowed time"));
            }
        }

        [Test]
        public void MessagesCanBeRouted()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var router = new HalibutRuntime(services, Certificates.TentaclePolling))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var routerPort = router.Listen();

                router.Trust(Certificates.OctopusPublicThumbprint);

                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.TentaclePollingPublicThumbprint);

                octopus.Route(
                    to: new ServiceEndPoint(new Uri("https://localhost:" + tentaclePort), Certificates.TentacleListeningPublicThumbprint),
                    via: new ServiceEndPoint(new Uri("https://localhost:" + routerPort), Certificates.TentaclePollingPublicThumbprint)
                    );

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                for (var i = 0; i < 100; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }
            }
        }

        [Test]
        public void StreamsCanBeSent()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                
                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                for (var i = 0; i < 100; i++)
                {
                    var count = echo.CountBytes(DataStream.FromBytes(data));
                    Assert.That(count, Is.EqualTo(1024 * 1024 + 15));
                }
            }
        }

        [Test]
        public void FailWhenServerThrowsAnException()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("at Halibut.Tests.Usage.EchoService.Crash()").And.StringContaining("divide by zero"));
            }
        }

        [Test]
        public void FailWhenServerThrowsAnExceptionOnPolling()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentaclePolling = new HalibutRuntime(services, Certificates.TentaclePolling))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Certificates.TentaclePollingPublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("at Halibut.Tests.Usage.EchoService.Crash()").And.StringContaining("divide by zero"));
            }
        }

        [Test]
        public void FailOnInvalidHostname()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request").And.StringContaining("No such host is known"));
            }
        }

        [Test]
        public void FailOnInvalidPort()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            {
                var echo = octopus.CreateClient<IEchoService>("https://google.com:88", Certificates.TentacleListeningPublicThumbprint);
                var ex = Assert.Throws<HalibutClientException>(() => echo.Crash());
                Assert.That(ex.Message, Is.StringContaining("when sending a request to 'https://google.com:88/', before the request").And.StringContaining("unable to establish the initial connection "));
            }
        }

        [Test]
        public void FailOnConnectionTearDown()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();

                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                EchoService.OnLongRunningOperation = () => tentacleListening.Dispose();

                var echo = octopus.CreateClient<IEchoService>("https://127.0.0.1:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.LongRunningOperation();
            }
        }

        [Test]
        public void FailWhenListeningClientPresentsWrongCertificate()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.TentaclePolling))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }

        [Test]
        public void FailWhenListeningServerPresentsWrongCertificate()
        {
            using (var octopus = new HalibutRuntime(services, Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentaclePollingPublicThumbprint);

                var ex = Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }

        [Test]
        public void SupportsDifferentServiceContractMethods()
        {
            services.Register<ISupportedServices>(() => new SupportedServices());
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);
                var tentaclePort = tentacleListening.Listen();

                var echo = octopus.CreateClient<ISupportedServices>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                echo.MethodReturningVoid(12, 14);

                Assert.That(echo.Hello(), Is.EqualTo("Hello"));
                Assert.That(echo.Hello("a"), Is.EqualTo("Hello a"));
                Assert.That(echo.Hello("a", "b"), Is.EqualTo("Hello a b"));
                Assert.That(echo.Hello("a", "b", "c"), Is.EqualTo("Hello a b c"));
                Assert.That(echo.Hello("a", "b", "c", "d"), Is.EqualTo("Hello a b c d"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e"), Is.EqualTo("Hello a b c d e"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f"), Is.EqualTo("Hello a b c d e f"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g"), Is.EqualTo("Hello a b c d e f g"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h"), Is.EqualTo("Hello a b c d e f g h"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i"), Is.EqualTo("Hello a b c d e f g h i"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j"), Is.EqualTo("Hello a b c d e f g h i j"));
                Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k"), Is.EqualTo("Hello a b c d e f g h i j k"));

                Assert.That(echo.Add(1, 2), Is.EqualTo(3));
                Assert.That(echo.Add(1.00, 2.00), Is.EqualTo(3.00));
                Assert.That(echo.Add(1.10M, 2.10M), Is.EqualTo(3.20M));

                Assert.That(echo.Ambiguous("a", "b"), Is.EqualTo("Hello string"));
                Assert.That(echo.Ambiguous("a", new Tuple<string, string>("a", "b")), Is.EqualTo("Hello tuple"));

                var ex = Assert.Throws<HalibutClientException>(() => echo.Ambiguous("a", (string)null));
                Assert.That(ex.Message, Is.StringContaining("Ambiguous"));
            }
        }

        #region Nested type: EchoService

        public class EchoService : IEchoService
        {
            public string SayHello(string name)
            {
                return name + "...";
            }

            public bool Crash()
            {
                throw new DivideByZeroException();
            }

            public static Action OnLongRunningOperation { get; set; }

            public int LongRunningOperation()
            {
                OnLongRunningOperation();
                Thread.Sleep(10000);
                return 12;
            }

            public int CountBytes(DataStream stream)
            {
                int read = 0;
                stream.Read(s =>
                {
                    while (s.ReadByte() != -1)
                    {
                        read++;
                    }
                });

                return read;
            }
        }

        #endregion

        #region Nested type: IEchoService

        public interface IEchoService
        {
            int LongRunningOperation();

            string SayHello(string name);

            bool Crash();

            int CountBytes(DataStream stream);
        }

        #endregion

        #region Nested type: ISupportedServices

        public interface ISupportedServices
        {
            void MethodReturningVoid(long a, long b);

            long Add(long a, long b);
            double Add(double a, double b);
            decimal Add(decimal a, decimal b);

            string Hello();
            string Hello(string a);
            string Hello(string a, string b);
            string Hello(string a, string b, string c);
            string Hello(string a, string b, string c, string d);
            string Hello(string a, string b, string c, string d, string e);
            string Hello(string a, string b, string c, string d, string e, string f);
            string Hello(string a, string b, string c, string d, string e, string f, string g);
            string Hello(string a, string b, string c, string d, string e, string f, string g, string h);
            string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i);
            string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j);
            string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k);

            string Ambiguous(string a, string b);
            string Ambiguous(string a, Tuple<string, string> b);
        }

        #endregion

        #region Nested type: SupportedServices

        public class SupportedServices : ISupportedServices
        {
            public void MethodReturningVoid(long a, long b)
            {
            }

            public long Add(long a, long b)
            {
                return a + b;
            }

            public double Add(double a, double b)
            {
                return a + b;
            }

            public decimal Add(decimal a, decimal b)
            {
                return a + b;
            }

            public string Hello()
            {
                return "Hello";
            }

            public string Hello(string a)
            {
                return "Hello " + a;
            }

            public string Hello(string a, string b)
            {
                return "Hello " + string.Join(" ", a, b);
            }

            public string Hello(string a, string b, string c)
            {
                return "Hello " + string.Join(" ", a, b, c);
            }

            public string Hello(string a, string b, string c, string d)
            {
                return "Hello " + string.Join(" ", a, b, c, d);
            }

            public string Hello(string a, string b, string c, string d, string e)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e);
            }

            public string Hello(string a, string b, string c, string d, string e, string f)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f);
            }

            public string Hello(string a, string b, string c, string d, string e, string f, string g)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f, g);
            }

            public string Hello(string a, string b, string c, string d, string e, string f, string g, string h)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h);
            }

            public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i);
            }

            public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j);
            }

            public string Hello(string a, string b, string c, string d, string e, string f, string g, string h, string i, string j, string k)
            {
                return "Hello " + string.Join(" ", a, b, c, d, e, f, g, h, i, j, k);
            }

            public string Ambiguous(string a, string b)
            {
                return "Hello string";
            }

            public string Ambiguous(string a, Tuple<string, string> b)
            {
                return "Hello tuple";
            }
        }

        #endregion
    }
}