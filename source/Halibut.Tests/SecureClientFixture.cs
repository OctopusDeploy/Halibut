using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NSubstitute;
using Xunit;

namespace Halibut.Tests
{
    public class SecureClientFixture
    {
        [Fact]
        public async Task SecureClientClearsPoolWhenAllConnectionsCorrupt()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());

            using (var tentacle = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacle.Listen();
                tentacle.Trust(Certificates.OctopusPublicThumbprint);

                var endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);
                var log = new InMemoryConnectionLog(endpoint.ToString());
                HalibutLimits.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;

                var pool = new ConnectionPool<ServiceEndPoint, IConnection>();
                var stream = Substitute.For<IMessageExchangeStream>();
                stream.When(x => x.IdentifyAsClient()).Do(x => throw new ConnectionInitializationFailedException(""));
                for (var i = 0; i < SecureClient.RetryCountLimit; i++)
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
                    Params = new object[] {"Fred"}
                };

                var secureClient = new SecureClient(endpoint, Certificates.Octopus, log, pool);
                ResponseMessage response = null;
                await secureClient.ExecuteTransaction(async (mep) => response = await mep.ExchangeAsClient(request).ConfigureAwait(false)).ConfigureAwait(false);

                // The pool should be cleared after the second failure
                await stream.Received(2).IdentifyAsClient().ConfigureAwait(false);
                // And a new valid connection should then be made
                response.Result.Should().Be("Fred...");

                await tentacle.Stop().ConfigureAwait(false);
            }
        }
    }
}