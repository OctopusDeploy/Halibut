using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Timeouts
{
    public class NextAndProceedControlMessageTimeoutsFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testListening: false)]
        public async Task PauseOnPollingTentacleSendingNextControlMessage_ShouldNotHangForEver(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var shouldPausePumpOnNextNextControlMessage = false;
            var sw = new Stopwatch();
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                             .AsLatestClientAndLatestServiceBuilder()
                             .WithPortForwarding(out var portForwarder)
                             .WithServiceControlMessageObserver(new FuncControlMessageObserver
                             {
                                 BeforeSendingControlMessageAction = controlMessage =>
                                 {
                                     if (shouldPausePumpOnNextNextControlMessage && controlMessage.Equals("NEXT"))
                                     {
                                         Logger.Information("Pausing pump");
                                         shouldPausePumpOnNextNextControlMessage = false;
                                         portForwarder.Value!.PauseExistingConnections();
                                         sw.Start();
                                     }
                                 }
                             })
                             .As<LatestClientAndLatestServiceBuilder>()
                             .WithEchoService()
                             .WithPollingReconnectRetryPolicy(() => new RetryPolicy(1, TimeSpan.Zero, TimeSpan.Zero))
                             .Build(CancellationToken))
            {
                var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

                await echo.SayHelloAsync(Some.RandomAsciiStringOfLength(2000));

                shouldPausePumpOnNextNextControlMessage = true;
                // When the port forwarder is paused the stopwatch is started,
                // we time the length it takes for recovery on that paused connection
                await echo.SayHelloAsync(Some.RandomAsciiStringOfLength(2000));
                await echo.SayHelloAsync(Some.RandomAsciiStringOfLength(2000)); // Do a second call just in case we missed the NEXT control message
                sw.Stop();
                Logger.Information("It took: " + sw.Elapsed);
                sw.Elapsed.Should().BeGreaterThanOrEqualTo(clientAndService.Service.TimeoutsAndLimits.TcpClientHeartbeatTimeout.ReceiveTimeout - TimeSpan.FromSeconds(2)) // -2s since tentacle will begin its countdown on the read which may start just after
                    // the response to the 'Second last message' is put back into the queue.
                    .And.BeLessThan(clientAndService.Service.TimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout, "Service should not be using this timeout to detect the control message is not coming back, it should use the shorter one.");
            }
        }
    }
}