using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BadCertificatesTests : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening:false)]
        public async Task SucceedsWhenPollingServicePresentsWrongCertificate_ButServiceIsConfiguredToTrustAndAllowConnection(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var countingService = new AsyncCountingService();
            var clientTrustProvider = new DefaultTrustProvider();
            var unauthorizedThumbprint = "";
            var firstCall = true;
            
            var unauthorizedClientHasConnected = new TaskCompletionSource<bool>();
            CancellationToken.Register(() => unauthorizedClientHasConnected.TrySetCanceled()); // backup to fail the test in case it never connects

            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithCountingService(countingService)
                       .WithClientTrustingTheWrongCertificate()
                       .WithClientTrustProvider(clientTrustProvider)
                       .WithClientOnUnauthorizedClientConnect((_, clientThumbprint) =>
                       {
                           if (firstCall)
                           {
                               clientTrustProvider.IsTrusted(CertAndThumbprint.TentaclePolling.Thumbprint).Should().BeFalse();
                               firstCall = false;
                           }

                           unauthorizedThumbprint = clientThumbprint;
                           unauthorizedClientHasConnected.TrySetResult(true);
                           return UnauthorizedClientConnectResponse.TrustAndAllowConnection;
                       })
                       .Build(CancellationToken))
            {
                // Act
                var clientCountingService = clientAndBuilder.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
                await clientCountingService.IncrementAsync();

                await unauthorizedClientHasConnected.Task;
                
                // Assert
                countingService.CurrentValue().Should().Be(1);

                clientTrustProvider.IsTrusted(CertAndThumbprint.TentaclePolling.Thumbprint).Should().BeTrue();
                unauthorizedThumbprint.Should().Be(CertAndThumbprint.TentaclePolling.Thumbprint);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task FailWhenPollingServicePresentsWrongCertificate_ButServiceIsConfiguredToBlockConnection(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            var countingService = new AsyncCountingService();
            var trustProvider = new DefaultTrustProvider();
            var unauthorizedThumbprint = "";

            var unauthorizedClientHasConnected = new TaskCompletionSource<bool>();
            CancellationToken.Register(() => unauthorizedClientHasConnected.TrySetCanceled()); // backup to fail the test in case it never connects

            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithCountingService(countingService)
                       .RecordingClientLogs(out var serviceLoggers)
                       .WithClientTrustingTheWrongCertificate()
                       .WithClientTrustProvider(trustProvider)
                       .WithClientOnUnauthorizedClientConnect((_, clientThumbprint) =>
                       {
                           unauthorizedThumbprint = clientThumbprint;
                           unauthorizedClientHasConnected.TrySetResult(true);
                           return UnauthorizedClientConnectResponse.BlockConnection;
                       })
                       .Build(CancellationToken))
            {
                using var cts = new CancellationTokenSource();
                var clientCountingService = clientAndBuilder.CreateAsyncClient<ICountingService, IAsyncClientCountingServiceWithOptions>(point =>
                {
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                });

                // Act
                var incrementCount = Task.Run(async () => await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)), CancellationToken);

                // Interestingly the message exchange error is logged to a non polling looking URL, perhaps because it has not been identified?
                Wait.UntilActionSucceeds(() => {
                        AllLogs(serviceLoggers).Select(l => l.FormattedMessage).ToArray()
                            .Should().Contain(s => s.Contains("and attempted a message exchange, but it presented a client certificate with the thumbprint '4098EC3A2FC2B92B97339D3831BA230CC1DD590F' which is not in the list of thumbprints that we trust"));
                    },
                    TimeSpan.FromSeconds(10),
                    Logger,
                    CancellationToken);
                
                await cts.CancelAsync();

                await AssertException.Throws<OperationCanceledException>(incrementCount);

                // don't assert things until we've seen the client actually connect
                await unauthorizedClientHasConnected.Task;
                
                // Assert
                countingService.CurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");

                unauthorizedThumbprint.Should().Be(CertAndThumbprint.TentaclePolling.Thumbprint);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(
            //Web sockets do not disconnect when calling TrustOnly. This issue has been raised.
            testWebSocket: false, 
            testNetworkConditions: false, testListening: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(
            //Web sockets do not disconnect when calling TrustOnly. This issue has been raised.
            testWebSocket: false,
            testNetworkConditions: false, testListening: false)]
        public async Task FailWhenPollingServiceHasThumbprintRemovedViaTrustOnly(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            // Arrange
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                using var cts = new CancellationTokenSource();
                var clientCountingService = clientAndBuilder
                    .CreateAsyncClient<ICountingService, IAsyncClientCountingServiceWithOptions>(point =>
                    {
                        point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000);
                    });

                // Works normally
                await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token));
                await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token));
                
                // Act
                clientAndBuilder.Client.TrustOnly(new List<string>());
                
                // Assert
                var incrementCount = Task.Run(async () => await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)), CancellationToken);

                await Task.Delay(3000, CancellationToken);

                await cts.CancelAsync();

                var exception = await AssertException.Throws<Exception>(incrementCount);

                exception.And.Should().Match(e => e.GetType() == typeof(HalibutClientException) 
                                                  || e is OperationCanceledException);
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testWebSocket: false, testPolling: false, testNetworkConditions: false)]
        public async Task FailWhenClientPresentsWrongCertificateToListeningService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new AsyncCountingService();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithServiceTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .RecordingServiceLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                var clientCountingService = clientAndBuilder.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
                await AssertionExtensions.Should(() => clientCountingService.IncrementAsync()).ThrowAsync<HalibutClientException>();

                countingService.CurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");

                serviceLoggers[serviceLoggers.Keys.First(x => x != nameof(MessageSerializer))].GetLogs().Should()
                    .Contain(log => log.FormattedMessage
                        .Contains("and attempted a message exchange, but it presented a client certificate with the thumbprint " +
                                  "'76225C0717A16C1D0BA4A7FFA76519D286D8A248' which is not in the list of thumbprints that we trust"));
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testNetworkConditions: false)]
        public async Task FailWhenClientPresentsWrongCertificateToPollingService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new AsyncCountingService();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithServiceTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .RecordingServiceLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                using var cts = new CancellationTokenSource();
                var clientCountingService = clientAndBuilder
                    .As<LatestClientAndLatestServiceBuilder.ClientAndService>()
                    .CreateAsyncClient<ICountingService, IAsyncClientCountingServiceWithOptions>(point =>
                {
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000);
                });
                
                var incrementCount = Task.Run(async () => await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)), CancellationToken);
                    
                Func<LogEvent, bool> hasExpectedLog = logEvent =>
                    logEvent.FormattedMessage.Contains("The server at")
                    && logEvent.FormattedMessage.Contains("presented an unexpected security certificate");

                Wait.UntilActionSucceeds(() => AllLogs(serviceLoggers).Should().Contain(log => hasExpectedLog(log)),
                    TimeSpan.FromSeconds(20),
                    Logger,
                    CancellationToken);
                
                await cts.CancelAsync();
                
                await AssertException.Throws<OperationCanceledException>(incrementCount);
                
                countingService.CurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false)]
        public async Task FailWhenListeningServicePresentsWrongCertificate(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new AsyncCountingService();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithClientTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .Build(CancellationToken))
            {
                var clientCountingService = clientAndBuilder.CreateAsyncClient<ICountingService, IAsyncClientCountingService>();
                (await AssertionExtensions.Should(() => clientCountingService.IncrementAsync()).ThrowAsync<HalibutClientException>())
                    .And.Message.Should().Contain("" +
                                                  "We expected the server to present a certificate with the thumbprint 'EC32122053C6BFF582F8246F5697633D06F0F97F'. " +
                                                  "Instead, it presented a certificate with a thumbprint of '36F35047CE8B000CF4C671819A2DD1AFCDE3403D'");
                countingService.CurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testNetworkConditions: false)]
        public async Task FailWhenPollingServicePresentsWrongCertificate(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new AsyncCountingService();
            await using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithClientTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .RecordingClientLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                using var cts = new CancellationTokenSource();
                var clientCountingService = clientAndBuilder.CreateAsyncClient<ICountingService, IAsyncClientCountingServiceWithOptions>(point =>
                {
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                });
                
                var incrementCount = Task.Run(async () => await clientCountingService.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)), CancellationToken);

                // Interestingly the message exchange error is logged to a non polling looking URL, perhaps because it has not been identified?
                Wait.UntilActionSucceeds(() => { AllLogs(serviceLoggers).Select(l => l.FormattedMessage).ToArray()
                        .Should().Contain(s => s.Contains("and attempted a message exchange, but it presented a client certificate with the thumbprint '4098EC3A2FC2B92B97339D3831BA230CC1DD590F' which is not in the list of thumbprints that we trust")); },
                    TimeSpan.FromSeconds(10),
                    Logger,
                    CancellationToken);
                

                await cts.CancelAsync();

                await AssertException.Throws<OperationCanceledException>(incrementCount);

                countingService.CurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }
        
        /// <summary>
        /// Test is redundant but kept around since we really want the security part to work. 
        /// </summary>
        [Test]
        public async Task FailWhenListeningClientPresentsWrongCertificateRedundant()
        {
            var services = GetDelegateServiceFactory();
            // The correct certificate would be Certificates.Octopus
            var wrongCertificate = Certificates.TentaclePolling;
            await using (var octopus = new HalibutRuntimeBuilder()
                             .WithServerCertificate(wrongCertificate)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            await using (var tentacleListening = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.TentacleListening)
                             .WithServiceFactory(services)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var serviceEndpoint = new ServiceEndPoint("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits);
                var echo = octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(serviceEndpoint);

                Assert.ThrowsAsync<HalibutClientException>(async () => await echo.SayHelloAsync("World"));
            }
        }

        /// <summary>
        /// Test is redundant but kept around since we really want the security part to work. 
        /// </summary>
        [Test]
        public async Task FailWhenListeningServerPresentsWrongCertificate()
        {
            var services = GetDelegateServiceFactory();
            await using (var octopus = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.Octopus)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            await using (var tentacleListening = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.TentacleListening)
                             .WithServiceFactory(services)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                // The correct one is Certificates.TentacleListeningPublicThumbprint
                var wrongThumbPrint = Certificates.TentaclePollingPublicThumbprint;

                var echo = octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint("https://localhost:" + tentaclePort, wrongThumbPrint, octopus.TimeoutsAndLimits));

                Assert.ThrowsAsync<HalibutClientException>(async () => await echo.SayHelloAsync("World"));
            }
        }
        
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
            return services;
        }

        IEnumerable<LogEvent> AllLogs(ConcurrentDictionary<string, ILog> loggers)
        {
            foreach (var key in loggers.Keys)
            {
                foreach (var logEvent in loggers[key].GetLogs())
                {
                    yield return logEvent;
                }
            }
        }
    }
}