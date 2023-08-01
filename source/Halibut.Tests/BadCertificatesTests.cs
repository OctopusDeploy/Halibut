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
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BadCertificatesTests : BaseTest
    {

        [Test]
        [LatestClientAndLatestServiceTestCases(testWebSocket: false, testPolling: false, testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task FailWhenClientPresentsWrongCertificateToListeningService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new CountingService();
            using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithServiceTrustingTheWrongCertificate()
                       .WithCountingService()
                       .RecordingServiceLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                var echo = clientAndBuilder.CreateClient<ICountingService, IAsyncClientCountingService>();
                Assert.ThrowsAsync<HalibutClientException>(async () => await echo.IncrementAsync());
                
                countingService.GetCurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
                
                serviceLoggers[serviceLoggers.Keys.First()].GetLogs().Should()
                    .Contain(log => log.FormattedMessage
                        .Contains("and attempted a message exchange, but it presented a client certificate with the thumbprint " +
                                  "'76225C0717A16C1D0BA4A7FFA76519D286D8A248' which is not in the list of thumbprints that we trust"));
            }
        }
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task FailWhenClientPresentsWrongCertificateToPollingService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new CountingService();
            using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithServiceTrustingTheWrongCertificate()
                       .WithCountingService()
                       .RecordingServiceLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                var cts = new CancellationTokenSource();
                var echo = clientAndBuilder
                    .As<LatestClientAndLatestServiceBuilder.ClientAndService>()
                    .CreateClientWithOptions<ICountingService, CancellationViaClientProxyFixture.IClientCountingService,  IAsyncClientCountingServiceWithOptions>(point =>
                {
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(2000);
                });
                
                var incrementCount = Task.Run(async () => await echo.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)));

                Func<LogEvent, bool> hasExpectedLog = logEvent =>
                    logEvent.FormattedMessage.Contains("The server at")
                    && logEvent.FormattedMessage.Contains("presented an unexpected security certificate");

                Wait.UntilActionSucceeds(() => AllLogs(serviceLoggers).Should().Contain(log => hasExpectedLog(log)),
                    TimeSpan.FromSeconds(20),
                    Logger,
                    CancellationToken);
                
                cts.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(async () => await incrementCount);

                countingService.GetCurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }
        
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task FailWhenListeningServicePresentsWrongCertificate(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new CountingService();
            using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithClientTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .Build(CancellationToken))
            {
                var echo = clientAndBuilder.CreateClient<ICountingService, IAsyncClientCountingService>();
                Assert.ThrowsAsync<HalibutClientException>(async () => await echo.IncrementAsync())
                    .Message.Should().Contain("" +
                                              "We expected the server to present a certificate with the thumbprint 'EC32122053C6BFF582F8246F5697633D06F0F97F'. " +
                                              "Instead, it presented a certificate with a thumbprint of '36F35047CE8B000CF4C671819A2DD1AFCDE3403D'");
                countingService.GetCurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }
        
        
        
        [Test]
        [LatestClientAndLatestServiceTestCases(testListening: false, testNetworkConditions: false, testAsyncAndSyncClients: true)]
        public async Task FailWhenPollingServicePresentsWrongCertificate(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var countingService = new CountingService();
            using (var clientAndBuilder = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .AsLatestClientAndLatestServiceBuilder()
                       .WithClientTrustingTheWrongCertificate()
                       .WithCountingService(countingService)
                       .RecordingClientLogs(out var serviceLoggers)
                       .Build(CancellationToken))
            {
                var cts = new CancellationTokenSource();
                var echo = clientAndBuilder.CreateClientWithOptions<ICountingService, CancellationViaClientProxyFixture.IClientCountingService, IAsyncClientCountingServiceWithOptions>(point =>
                {
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(10);
                });
                
                var incrementCount = Task.Run(async () => await echo.IncrementAsync(new HalibutProxyRequestOptions(cts.Token)));

                // Interestingly the message exchange error is logged to a non polling looking URL, perhaps because it has not been identified?
                Wait.UntilActionSucceeds(() => { AllLogs(serviceLoggers).Select(l => l.FormattedMessage).ToArray()
                        .Should().Contain(s => s.Contains("and attempted a message exchange, but it presented a client certificate with the thumbprint '4098EC3A2FC2B92B97339D3831BA230CC1DD590F' which is not in the list of thumbprints that we trust")); },
                    TimeSpan.FromSeconds(10),
                    Logger,
                    CancellationToken);
                

                cts.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(async () => await incrementCount);

                countingService.GetCurrentValue().Should().Be(0, "With a bad certificate the request never should have been made");
            }
        }

        private IEnumerable<LogEvent> AllLogs(ConcurrentDictionary<string, ILog> loggers)
        {
            foreach (var key in loggers.Keys)
            {
                foreach (var logEvent in loggers[key].GetLogs())
                {
                    yield return logEvent;
                }
            }
        }



        /// <summary>
        /// Test is redundant but kept around since we really want the security part to work. 
        /// </summary>
        [Test]
        public void FailWhenListeningClientPresentsWrongCertificateRedundant()
        {
            var services = GetDelegateServiceFactory();
            // The correct certificate would be Certificates.Octopus
            var wrongCertificate = Certificates.TentaclePolling;
            using (var octopus = new HalibutRuntime(wrongCertificate))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, Certificates.TentacleListeningPublicThumbprint);

                Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }

        /// <summary>
        /// Test is redundant but kept around since we really want the security part to work. 
        /// </summary>
        [Test]
        public void FailWhenListeningServerPresentsWrongCertificate()
        {
            var services = GetDelegateServiceFactory();
            using (var octopus = new HalibutRuntime(Certificates.Octopus))
            using (var tentacleListening = new HalibutRuntime(services, Certificates.TentacleListening))
            {
                var tentaclePort = tentacleListening.Listen();
                tentacleListening.Trust(Certificates.OctopusPublicThumbprint);

                // The correct one is Certificates.TentacleListeningPublicThumbprint
                var wrongThumbPrint = Certificates.TentaclePollingPublicThumbprint;

                var echo = octopus.CreateClient<IEchoService>("https://localhost:" + tentaclePort, wrongThumbPrint);

                Assert.Throws<HalibutClientException>(() => echo.SayHello("World"));
            }
        }
        
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }
    }
}