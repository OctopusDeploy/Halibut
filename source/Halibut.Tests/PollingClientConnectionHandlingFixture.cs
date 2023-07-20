﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    [NonParallelizable]
    public class PollingClientConnectionHandlingFixture : BaseTest
    {
        [Test]
        [TestCase(ServiceConnectionType.Polling)]
#if SUPPORTS_WEB_SOCKET_CLIENT
        [TestCase(ServiceConnectionType.PollingOverWebSocket)]
#endif
        public async Task PollingClientShouldConnectQuickly(ServiceConnectionType serviceConnectionType)
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService) = await SetupPollingServerAndTentacle(serviceConnectionType, () =>
            {
                calls.Add(DateTime.UtcNow);
            });

            using (clientAndService)
            {
                doSomeActionService.Action();
            }

            calls.Should().HaveCount(1);
            calls.ElementAt(0).Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(serviceConnectionType == ServiceConnectionType.Polling ? 5 : 30));
        }

        [Test]
        [TestCase(ServiceConnectionType.Polling)]
#if SUPPORTS_WEB_SOCKET_CLIENT
        [TestCase(ServiceConnectionType.PollingOverWebSocket)]
#endif
        public async Task PollingClientShouldReConnectQuickly(ServiceConnectionType serviceConnectionType)
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService) = await SetupPollingServerAndTentacle(serviceConnectionType, () =>
            {
                calls.Add(DateTime.UtcNow);
            });

            using (clientAndService)
            {
                doSomeActionService.Action();

                clientAndService.PortForwarder!.CloseExistingConnections();

                // First Reconnect
                try
                {
                    doSomeActionService.Action();
                }
                catch (HalibutClientException ex)
                {
                    // Work around the known dequeue to a broken tcp connection issue
                    doSomeActionService.Action();
                }

                clientAndService.PortForwarder!.CloseExistingConnections();

                // Second Reconnect
                try
                {
                    doSomeActionService.Action();
                }
                catch (HalibutClientException ex)
                {
                    // Work around the known dequeue to a broken tcp connection issue
                    doSomeActionService.Action();
                }
            }

            calls.Should().HaveCount(3);

            var firstCall = calls.ElementAt(0);
            var firstReconnect = calls.ElementAt(1);
            var secondReconnect = calls.ElementAt(2);

            firstCall.Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(serviceConnectionType == ServiceConnectionType.Polling ? 5 : 30));
            firstReconnect.Should().BeOnOrAfter(firstCall).And.BeCloseTo(firstCall, TimeSpan.FromSeconds(serviceConnectionType == ServiceConnectionType.Polling ? 8 : 30));
            secondReconnect.Should().BeOnOrAfter(firstReconnect).And.BeCloseTo(firstReconnect, TimeSpan.FromSeconds(serviceConnectionType == ServiceConnectionType.Polling ? 8 : 30));
        }

        async Task<(LatestClientAndLatestServiceBuilder.ClientAndService,
                IEchoService echoService,
                IDoSomeActionService doSomeActionService)>
            SetupPollingServerAndTentacle(ServiceConnectionType serviceConnectionType, Action doSomeActionServiceAction)
        {
            var clientAndService = await LatestClientAndLatestServiceBuilder
                .ForServiceConnectionType(serviceConnectionType)
                .WithPortForwarding()
                .WithDoSomeActionService(doSomeActionServiceAction)
                .WithEchoService()
                .Build(CancellationToken);

            var doSomeActionService = clientAndService.CreateClient<IDoSomeActionService>();
            var echoService = clientAndService.CreateClient<IEchoService>();

            EnsureTentacleIsConnected(echoService);

            return (clientAndService, echoService, doSomeActionService);
        }

        static void EnsureTentacleIsConnected(IEchoService echoService)
        {
            // Ensure the tentacle is connected
            echoService.SayHello("Hello");
            // Ensure the tentacle is waiting for the next request
            Thread.Sleep(2);
        }
    }
}
