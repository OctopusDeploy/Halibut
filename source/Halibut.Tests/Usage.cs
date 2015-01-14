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
using System.Diagnostics;
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
                for (var i = 0; i < 20; i++)
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
                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Certificates.OctopusPublicThumbprint));

                var echo = octopus.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);
                for (var i = 0; i < 100; i++)
                {
                    Assert.That(echo.SayHello("Deploy package A"), Is.EqualTo("Deploy package A..."));
                }
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

                var count = echo.CountBytes(DataStream.FromBytes(data));
                Assert.That(count, Is.EqualTo(1024 * 1024 + 15));
            }
        }

        //[Test]
        //public void AliceOnlySendsMessagesToBob()
        //{
        //    using (var eve = new HalibutServer(new IPEndPoint(IPAddress.Any, 8013), Certificates.Eve))
        //    {
        //        eve.Services.Register<IEchoService, EchoService>();
        //        eve.Options.ClientCertificateValidator = v => v.Thumbprint == Certificates.AlicePublicThumbprint ? CertificateValidationResult.Valid : CertificateValidationResult.Rejected;
        //        eve.Start();

        //        var alice = new HalibutClient(Certificates.Alice);
        //        var echo = alice.Create<IEchoService>(new Uri("rpc://localhost:8013"), Certificates.BobPublicThumbprint);

        //        var ex = Assert.Throws<JsonRpcException>(() => echo.SayHello("Hi Bob, it's Eve"));
        //        Assert.That(ex.Message, Is.StringContaining("We aborted the connection because the remote host was not authenticated"));
        //    }
        //}

        //[Test]
        //public void BobOnlyAcceptsMessagesFromAlice()
        //{
        //    using (var bob = new HalibutServer(new IPEndPoint(IPAddress.Any, 8013), Certificates.Bob))
        //    {
        //        bob.Services.Register<IEchoService, EchoService>();
        //        bob.Options.ClientCertificateValidator = v => v.Thumbprint == Certificates.AlicePublicThumbprint ? CertificateValidationResult.Valid : CertificateValidationResult.Rejected;
        //        bob.Start();

        //        var eve = new HalibutClient(Certificates.Eve);
        //        var echo = eve.Create<IEchoService>(new Uri("rpc://localhost:8013"), Certificates.BobPublicThumbprint);

        //        var ex = Assert.Throws<JsonRpcException>(() => echo.SayHello("Hi Bob, it's Eve"));
        //        Assert.That(ex.Message, Is.StringContaining("This can happen when the remote server does not trust the certificate that we provided."));
        //    }
        //}

        //[Test]
        //public void ServerExceptionsAreWrappedAsJsonExceptions()
        //{
        //    using (var bob = new HalibutServer(new IPEndPoint(IPAddress.Any, 8013), Certificates.Bob))
        //    {
        //        bob.Services.Register<IEchoService, EchoService>();
        //        bob.Options.ClientCertificateValidator = v => v.Thumbprint == Certificates.AlicePublicThumbprint ? CertificateValidationResult.Valid : CertificateValidationResult.Rejected;
        //        bob.Start();

        //        var alice = new HalibutClient(Certificates.Alice);
        //        var echo = alice.Create<IEchoService>(new Uri("rpc://localhost:8013"), Certificates.BobPublicThumbprint);

        //        var jsonex = Assert.Throws<JsonRpcException>(() => echo.Crash());
        //        Assert.That(jsonex.Message, Is.StringContaining("divide by zero"));
        //    }
        //}

        //[Test]
        //public void SupportsDifferentServiceContractMethods()
        //{
        //    using (var bob = new HalibutServer(new IPEndPoint(IPAddress.Any, 8013), Certificates.Bob))
        //    {
        //        bob.Services.Register<ISupportedServices, SupportedServices>();
        //        bob.Options.ClientCertificateValidator = v => v.Thumbprint == Certificates.AlicePublicThumbprint ? CertificateValidationResult.Valid : CertificateValidationResult.Rejected;
        //        bob.Start();

        //        var alice = new HalibutClient(Certificates.Alice);
        //        var echo = alice.Create<ISupportedServices>(new Uri("rpc://localhost:8013"), Certificates.BobPublicThumbprint);

        //        echo.MethodReturningVoid(12, 14);

        //        Assert.That(echo.Hello(), Is.EqualTo("Hello"));
        //        Assert.That(echo.Hello("a"), Is.EqualTo("Hello a"));
        //        Assert.That(echo.Hello("a", "b"), Is.EqualTo("Hello a b"));
        //        Assert.That(echo.Hello("a", "b", "c"), Is.EqualTo("Hello a b c"));
        //        Assert.That(echo.Hello("a", "b", "c", "d"), Is.EqualTo("Hello a b c d"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e"), Is.EqualTo("Hello a b c d e"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f"), Is.EqualTo("Hello a b c d e f"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g"), Is.EqualTo("Hello a b c d e f g"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h"), Is.EqualTo("Hello a b c d e f g h"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i"), Is.EqualTo("Hello a b c d e f g h i"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j"), Is.EqualTo("Hello a b c d e f g h i j"));
        //        Assert.That(echo.Hello("a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k"), Is.EqualTo("Hello a b c d e f g h i j k"));

        //        Assert.That(echo.Add(1, 2), Is.EqualTo(3));
        //        Assert.That(echo.Add(1.00, 2.00), Is.EqualTo(3.00));
        //        Assert.That(echo.Add(1.10M, 2.10M), Is.EqualTo(3.20M));

        //        Assert.That(echo.Ambiguous("a", "b"), Is.EqualTo("Hello string"));
        //        Assert.That(echo.Ambiguous("a", new Tuple<string, string>("a", "b")), Is.EqualTo("Hello tuple"));

        //        var ex = Assert.Throws<JsonRpcException>(() => echo.Ambiguous("a", (string) null));
        //        Assert.That(ex.Message, Is.StringContaining("Ambiguous"));
        //    }
        //}

        //[Test]
        //public void X()
        //{
        //    var serializer = new JsonSerializer();

        //    var data = new byte[1024];
        //    new Random().NextBytes(data);
        //    var data2 = new byte[1024];
        //    new Random().NextBytes(data2);

        //    using (var capture = StreamCapture.New())
        //    {
        //        using (var ms = new MemoryStream())
        //        using (var writer = new JsonTextWriter(new StreamWriter(ms)))
        //        {
        //            serializer.ContractResolver = new PaulCamelCasePropertyNamesContractResolver();
        //            serializer.Serialize(writer, new PackageCollection
        //            {
        //                Packages = new []
        //                {
        //                    DataStream.FromBytes(data),
        //                    DataStream.FromBytes(data2)
        //                }
        //            });
        //        }

        //        Assert.That(capture.SerializedStreams.Count, Is.EqualTo(2));
        //        Assert.That(capture.DeserializedStreams.Count, Is.EqualTo(2));
        //    }
        //}

        public class PackageCollection
        {
            public DataStream[] Packages { get; set; }
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