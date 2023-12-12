using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class PollingClientConnectionHandlingFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task PollingClientShouldConnectQuickly(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService, _) = await SetupPollingServerAndTentacle(clientAndServiceTestCase, () =>
            {
                calls.Add(DateTime.UtcNow);
            });

            await using (clientAndService)
            {
                await doSomeActionService.ActionAsync();
            }

            calls.Should().HaveCount(1);
            calls.ElementAt(0).Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.Polling ? 5 : 30));
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task PollingClientShouldReConnectQuickly_WhenTheLastConnectionAttemptWasASuccess(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService, portForwarderRef) = await SetupPollingServerAndTentacle(clientAndServiceTestCase, () =>
            {
                calls.Add(DateTime.UtcNow);
            });

            await using (clientAndService)
            {
                await doSomeActionService.ActionAsync();

                portForwarderRef.Value.CloseExistingConnections();

                // First Reconnect
                try
                {
                    await doSomeActionService.ActionAsync();
                }
                catch (HalibutClientException)
                {
                    // Work around the known dequeue to a broken tcp connection issue
                    await doSomeActionService.ActionAsync();
                }

                portForwarderRef.Value.CloseExistingConnections();

                // Second Reconnect
                try
                {
                    await doSomeActionService.ActionAsync();
                }
                catch (HalibutClientException)
                {
                    // Work around the known dequeue to a broken tcp connection issue
                    await doSomeActionService.ActionAsync();
                }
            }

            calls.Should().HaveCount(3);

            var firstCall = calls.ElementAt(0);
            var firstReconnect = calls.ElementAt(1);
            var secondReconnect = calls.ElementAt(2);

            // It should take way less than this to reconnect, however we measure the time to close the socket which can take a long time.
            // The retry policy is setup such that if the last attempt is not recorded as a success, it will return a very high sleep causing this test to fail.
            var expectedMaxDuration = TimeSpan.FromSeconds(30);
            firstReconnect.Should().BeOnOrAfter(firstCall).And.BeCloseTo(firstCall, expectedMaxDuration);
            secondReconnect.Should().BeOnOrAfter(firstReconnect).And.BeCloseTo(firstReconnect, expectedMaxDuration);
        }

        async Task<(LatestClientAndLatestServiceBuilder.ClientAndService,
                IAsyncClientEchoService echoService,
                IAsyncClientDoSomeActionService doSomeActionService,
                Reference<PortForwarder> portForwarderRef)>
            SetupPollingServerAndTentacle(ClientAndServiceTestCase clientAndServiceTestCase, Action doSomeActionServiceAction)
        {
            var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .As<LatestClientAndLatestServiceBuilder>()
                .WithPortForwarding(out var portForwarderRef)
                .WithDoSomeActionService(doSomeActionServiceAction)
                .WithEchoService()
                .WithPollingReconnectRetryPolicy(() => new RetryPolicy(99999999, TimeSpan.Zero, TimeSpan.FromMinutes(1)))
                .Build(CancellationToken);

            var doSomeActionService = clientAndService.CreateAsyncClient<IDoSomeActionService, IAsyncClientDoSomeActionService>();
            var echoService = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

            await EnsureTentacleIsConnected(echoService);

            return (clientAndService, echoService, doSomeActionService, portForwarderRef);
        }

        static async Task EnsureTentacleIsConnected(IAsyncClientEchoService echoService)
        {
            // Ensure the tentacle is connected
            await echoService.SayHelloAsync("Hello");
            // Ensure the tentacle is waiting for the next request
            await Task.Delay(2);
        }
    }
}
