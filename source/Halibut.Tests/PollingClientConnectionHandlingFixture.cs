using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;
using Octopus.TestPortForwarder;

namespace Halibut.Tests
{
    public class PollingClientConnectionHandlingFixture
    {
        [Test]
        public void PollingClientShouldConnectQuickly()
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService) = SetupPollingServerAndTentacle(() =>
            {
                calls.Add(DateTime.UtcNow);
            });
            
            using (clientAndService)
            {
                doSomeActionService.Action();
            }

            calls.Should().HaveCount(1);
            calls.ElementAt(0).Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void PollingClientShouldReConnectQuickly()
        {
            var started = DateTime.UtcNow;
            var calls = new List<DateTime>();

            var (clientAndService, _, doSomeActionService) = SetupPollingServerAndTentacle(() =>
            {
                calls.Add(DateTime.UtcNow);
            });
            
            using (clientAndService)
            {
                doSomeActionService.Action();

                clientAndService.portForwarder.CloseExistingConnections();

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

                clientAndService.portForwarder.CloseExistingConnections();

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

            firstCall.Should().BeOnOrAfter(started).And.BeCloseTo(started, TimeSpan.FromSeconds(5));
            firstReconnect.Should().BeOnOrAfter(firstCall).And.BeCloseTo(firstCall, TimeSpan.FromSeconds(8));
            secondReconnect.Should().BeOnOrAfter(firstReconnect).And.BeCloseTo(firstReconnect, TimeSpan.FromSeconds(8));
        }

        (ClientServiceBuilder.ClientAndService,
            IEchoService echoService,
            IDoSomeActionService doSomeActionService)
            SetupPollingServerAndTentacle(Action doSomeActionServiceAction)
        {
            var clientAndService = ClientServiceBuilder.Polling().WithPortForwarding()
                .WithService<IDoSomeActionService>(() => new DoSomeActionService(doSomeActionServiceAction))
                .WithService<IEchoService>(() => new EchoService())
                .Build();
            
            
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