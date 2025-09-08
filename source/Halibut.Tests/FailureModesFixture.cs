using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class FailureModesFixture : BaseTest
    {
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());
            return services;
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task FailsWhenSendingToPollingMachineButNothingPicksItUp(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var client = await clientAndServiceTestCase.CreateClientOnlyTestCaseBuilder().Build(CancellationToken))
            {
                var echo = client.CreateClientWithoutService<IEchoService, IAsyncClientEchoService>(point =>
                {
                    point.TcpClientConnectTimeout = TimeSpan.FromSeconds(1);
                    point.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
                });


                var error = (await AssertException.Throws<HalibutClientException>(() => echo.SayHelloAsync("Paul"))).And;
                error.Message.Should().Contain("the polling endpoint did not collect the request within the allowed time");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task FailWhenServerThrowsAnException(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                var ex = (await AssertException.Throws<ServiceInvocationHalibutClientException>(() => echo.CrashAsync())).And;
                if (clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousService())
                {
                    ex.Message.Should().Contain("at Halibut.TestUtils.SampleProgram.Base.Services.EchoService.Crash()").And.Contain("divide by zero");
                }
                else
                {
                    var expected = "at Halibut.Tests.TestServices.AsyncEchoService.CrashAsync(";
#if NETFRAMEWORK
                    expected = "at Halibut.Tests.TestServices.AsyncEchoService.<CrashAsync>";
#endif
                    ex.Message.Should().Contain(expected).And.Contain("divide by zero");
                }
                
            }
        }

        /// <summary>
        /// InvocationServiceException is how clients know that the failure occured within the service, so we check this is returned in the ResponseMessage
        /// </summary>
        /// <param name="clientAndServiceTestCase"></param>
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [PreviousClientAndLatestServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task FailsWithInvocationServiceExceptionWhenAsyncServiceCrashes(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .WithAsyncService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService())
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();
                var ex = (await AssertException.Throws<ServiceInvocationHalibutClientException>(() => echo.CrashAsync())).And;
                var expected = "at Halibut.Tests.TestServices.AsyncEchoService.CrashAsync(";
#if NETFRAMEWORK
                    expected = "at Halibut.Tests.TestServices.AsyncEchoService.<CrashAsync>";
#endif
                ex.Message.Should().Contain(expected).And.Contain("divide by zero");

                if (clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient())
                {
                    // This here verifies that the client actually did see something that looks like a service invocation exception.
                    ex.Message.Replace("\r", "")
                        .Replace("\n", "")
                        .Should()
                        .Contain(@"Received Exception: START:
Attempted to divide by zero.

Server exception: 
System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.
 ---> System.DivideByZeroException: Attempted to divide by zero".Replace("\r", "").Replace("\n", ""));
                }
            }
        }

        [Test]
        public async Task FailOnInvalidHostname()
        {
            var services = GetDelegateServiceFactory();
            await using (var octopus = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.Octopus)
                             .WithServiceFactory(services)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var echo = octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(new ServiceEndPoint("https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000", Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits));
                var ex = Assert.ThrowsAsync<HalibutClientException>(async () => await echo.CrashAsync());
                var message = ex!.Message;

                message.Should().Contain("when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message.Should().Contain("No such host is known");
                }
                else
                {
                    // Failed with: An error occurred when sending a request to 'https://sduj08ud9382ujd98dw9fh934hdj2389u982:8000/', before the request could begin: Name or service not known, but found False.
                    new [] {"No such device or address", "Resource temporarily unavailable", "Name or service not known"}.Any(message.Contains).Should().BeTrue($"Message does not match known strings: {message}");
                }
            }
        }

        [Test]
        public async Task FailOnInvalidPort()
        {
            var services = GetDelegateServiceFactory();
            await using (var octopus = new HalibutRuntimeBuilder()
                             .WithServerCertificate(Certificates.Octopus)
                             .WithServiceFactory(services)
                             .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                             .Build())
            {
                var endpoint = new ServiceEndPoint("https://google.com:88", Certificates.TentacleListeningPublicThumbprint, octopus.TimeoutsAndLimits)
                {
                    TcpClientConnectTimeout = TimeSpan.FromSeconds(2),
                    RetryCountLimit = 2
                };
                var echo = octopus.CreateAsyncClient<IEchoService, IAsyncClientEchoService>(endpoint);
                var ex = Assert.ThrowsAsync<HalibutClientException>(async () => await echo.CrashAsync());
                ex!.Message.Should().Be("An error occurred when sending a request to 'https://google.com:88/', before the request could begin: The client was unable to establish the initial connection within the timeout 00:00:02.");
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task FailWhenServerThrowsDuringADataStream(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithStandardServices()
                       .WithHalibutLoggingLevel(LogLevel.Fatal)
                       .As<LatestClientAndLatestServiceBuilder>()
                       .WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero))
                       .Build(CancellationToken))
            {
                var readDataSteamService = clientAndService.CreateAsyncClient<IReadDataStreamService, IAsyncClientReadDataStreamService>();

                // Previously tentacle would eventually stop responding only after many failed calls.
                // This loop ensures (at the time) the test shows the problem.
                for (var i = 0; i < 128; i++)
                {
                    await AssertException.Throws<HalibutClientException>(async () => await readDataSteamService.SendDataAsync(
                        new DataStream(10000, 
                            async (_, _) =>
                                {
                                    await Task.CompletedTask;
                                    throw new Exception("Oh noes");
                                })));
                }

                var received = await readDataSteamService.SendDataAsync(DataStream.FromString("hello"));
                received.Should().Be(5);
            }
        }
    }
}
