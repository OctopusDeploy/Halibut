﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Transport
{
    public class SecureClientFixture : IDisposable
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
            endpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint)
            {
                ConnectionErrorRetryTimeout = TimeSpan.MaxValue
            };
            log = TestContextLogFactory.CreateTestLog("Client", LogLevel.Info).ForEndpoint(endpoint.BaseUri);
        }

        public void Dispose()
        {
            tentacle.Dispose();
        }

        [Test]
        [SyncAndAsync]
        public async Task SecureClientClearsPoolWhenAllConnectionsCorrupt(SyncOrAsync syncOrAsync)
        {
            using var connectionManager = syncOrAsync.CreateConnectionManager();
            var stream = Substitute.For<IMessageExchangeStream>();

            syncOrAsync
                .WhenSync(() => stream.When(x => x.IdentifyAsClient()).Do(x => throw new ConnectionInitializationFailedException("")))
                .WhenAsync(() => stream.IdentifyAsClientAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConnectionInitializationFailedException(""))));
            
            for (int i = 0; i < HalibutLimits.RetryCountLimit; i++)
            {
                var connection = Substitute.For<IConnection>();
                connection.Protocol.Returns(new MessageExchangeProtocol(stream, log));
                connectionManager.ReleaseConnection(endpoint, connection);
            }

            var request = new RequestMessage
            {
                Destination = endpoint,
                ServiceName = "IEchoService",
                MethodName = "SayHello",
                Params = new object[] { "Fred" }
            };

            var secureClient = new SecureListeningClient((s, l)  => GetProtocol(s, l, syncOrAsync), endpoint, Certificates.Octopus, new HalibutTimeoutsAndLimits(), log, connectionManager);
            ResponseMessage response = null!;

            using var requestCancellationTokens = new RequestCancellationTokens(CancellationToken.None, CancellationToken.None);

#pragma warning disable CS0612
            await syncOrAsync
                .WhenSync(() => secureClient.ExecuteTransaction((mep) => response = mep.ExchangeAsClient(request), CancellationToken.None))
                .WhenAsync(async () => await secureClient.ExecuteTransactionAsync(async (mep, ct) => response = await mep.ExchangeAsClientAsync(request, ct), requestCancellationTokens));
#pragma warning restore CS0612

            // The pool should be cleared after the second failure
            await syncOrAsync
                .WhenSync(() => stream.Received(2).IdentifyAsClient())
                .WhenAsync(async () => await stream.Received(2).IdentifyAsClientAsync(Arg.Any<CancellationToken>()));
            // And a new valid connection should then be made
            response.Result.Should().Be("Fred...");
        }

        public MessageExchangeProtocol GetProtocol(Stream stream, ILog logger, SyncOrAsync syncOrAsync)
        {
            return new MessageExchangeProtocol(new MessageExchangeStream(stream, new MessageSerializerBuilder(new LogFactory()).Build(), syncOrAsync.ToAsyncHalibutFeature(), new HalibutTimeoutsAndLimits(), logger), logger);
        }
    }
}
