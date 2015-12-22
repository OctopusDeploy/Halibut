using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class SecureClientFixture
    {
        ServiceEndPoint endpoint;
        HalibutRuntime tentacle;
        ILog log;

        [SetUp]
        public void SetUp()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            tentacle = new HalibutRuntime(services, Certificates.TentacleListening);
            var tentaclePort = tentacle.Listen();
            tentacle.Trust(Certificates.OctopusPublicThumbprint);
            endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
            log = new InMemoryConnectionLog(endpoint.ToString());
            HalibutLimits.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
        }

        [TearDown]
        public void TearDown()
        {
            tentacle.Dispose();
        }

        [Test]
        public void SecureClientClearsPoolWhenAllConnectionsCorrupt()
        {
            var pool = new ConnectionPool<ServiceEndPoint, IConnection>();
            var stream = Substitute.For<IMessageExchangeStream>();
            stream.When(x => x.IdentifyAsClient()).Do(x => { throw new ConnectionInitializationFailedException(""); });
            for (int i = 0; i < SecureClient.RetryCountLimit; i++)
            {
                var connection = Substitute.For<IConnection>();
                connection.Protocol.Returns(new MessageExchangeProtocol(stream));
                pool.Return(endpoint, connection);
            }

            var request = new RequestMessage
            {
                Destination = endpoint,
                ServiceName = "IEchoService",
                MethodName = "SayHello",
                Params = new object[] { "Fred" }
            };

            var secureClient = new SecureClient(endpoint, Certificates.Octopus, log, pool);
            ResponseMessage response = null;
            secureClient.ExecuteTransaction((mep) => response = mep.ExchangeAsClient(request));

            // The pool should be cleared after the second failure
            stream.Received(2).IdentifyAsClient();
            // And a new valid connection should then be made
            Assert.AreEqual("Fred...", response.Result);
        }
    }
}