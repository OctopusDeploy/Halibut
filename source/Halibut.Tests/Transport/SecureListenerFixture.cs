using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    [NonParallelizable]
    public class SecureListenerFixture
    {
        PerformanceCounter GetCounterForCurrentProcess(string categoryName, string counterName)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var pid = Process.GetCurrentProcess().Id;

            var instanceName = new PerformanceCounterCategory("Process")
                .GetInstanceNames()
                .FirstOrDefault(instance =>
                {

                    using (var counter = new PerformanceCounter("Process", "ID Process", instance, true))
                    {
                        try
                        {
                            return pid == counter.RawValue;
                        }
                        catch (InvalidOperationException)
                        {
                            return false;
                        }
                    }
                });

            if (instanceName == null)
            {
                throw new Exception("Could not find instance name for process.");
            }

            return new PerformanceCounter(categoryName, counterName, instanceName, true);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        [Test]
        [WindowsTest]
        public async Task SecureListenerDoesNotCreateHundredsOfIoEventsPerSecondOnWindows()
        {
            var logger = new SerilogLoggerBuilder().Build();
            const int secondsToSample = 5;

            using (var opsPerSec = GetCounterForCurrentProcess("Process", "IO Other Operations/sec"))
            {
                var timeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
                var client = new SecureListener(
                    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 0),
                    Certificates.TentacleListening,
                    null!,
                    null!,
                    _ => true,
                    new LogFactory(),
                    () => "",
                    () => new Dictionary<string, string>(),
                    (_, _) => UnauthorizedClientConnectResponse.BlockConnection,
                    timeoutsAndLimits,
                    new StreamFactory(),
                    NoOpConnectionsObserver.Instance,
                    NoOpSecureConnectionObserver.Instance
                );

                var idleAverage = CollectCounterValues(opsPerSec)
                    .Take(secondsToSample)
                    .Average();

                float listeningAverage;

                await using (client)
                {
                    client.Start();

                    listeningAverage = CollectCounterValues(opsPerSec)
                        .Take(secondsToSample)
                        .Average();
                }

                var idleAverageWithErrorMargin = idleAverage * 250f;

                logger.Information($"idle average:      {idleAverage} ops/second");
                logger.Information($"listening average: {listeningAverage} ops/second");
                logger.Information($"expectation:     < {idleAverageWithErrorMargin} ops/second");

                listeningAverage.Should().BeLessThan(idleAverageWithErrorMargin);
            }
        }

        IEnumerable<float> CollectCounterValues(PerformanceCounter counter)
        {
            var sleepTime = TimeSpan.FromSeconds(1);

            while (true)
            {
                Thread.Sleep(sleepTime);
#pragma warning disable CA1416 // Validate platform compatibility
                yield return counter.NextValue();
#pragma warning restore CA1416 // Validate platform compatibility
            }
            // ReSharper disable once IteratorNeverReturns : Take is limit
        }

        static IEnumerable<int> NumberOfAttempts = Enumerable.Range(0, 20).ToArray();
        
        
        [Test]
        [TestCaseSource(nameof(NumberOfAttempts))]
        public async Task CanShutdownWhileAcceptingNewConnections(int _)
        {
            await Task.WhenAll(Enumerable.Range(0, Environment.ProcessorCount)
                .Select(i => Task.Run(() => ShutdownWhileAcceptingConnections()))
                .ToArray());
            
        }

        static async Task ShutdownWhileAcceptingConnections()
        {
            var limits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            limits.UseAsyncListener = true;
            var cts = new CancellationTokenSource();
            var connectTasks = new List<Task>();
            using (var halibut = new HalibutRuntimeBuilder().WithHalibutTimeoutsAndLimits(limits)
                       .WithServerCertificate(CertAndThumbprint.Octopus.Certificate2)
                       .Build())
            {
                var port = halibut.Listen();

                for (int i = 0; i < 64; i++)
                {
                    connectTasks.Add(Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                await ConnectAndDisconnect(limits, port);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }));
                }
                
                // Let's do one ourselves, to kill time and verify this call does work.
                await ConnectAndDisconnect(limits, port);
                // A little more time for tasks to start.
                await Task.Delay(TimeSpan.FromMilliseconds(30));
            } // Here is the test we dispose/stop the listner. We should not hang here forever.
            
#if NET8_0_OR_GREATER
            await cts.CancelAsync();
#else
            cts.Cancel();
#endif
            foreach (var connectTask in connectTasks)
            {
                await connectTask;
            }
        }

        static async Task ConnectAndDisconnect(HalibutTimeoutsAndLimits limits, int port)
        {
            var tcpClient = CreateTcpClientAsync(limits);
            await tcpClient.ConnectAsync("localhost", port);
            tcpClient.Client.Shutdown(SocketShutdown.Both);
            tcpClient.Dispose();
        }

        internal static TcpClient CreateTcpClientAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var addressFamily = Socket.OSSupportsIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork;

            return CreateTcpClientAsync(addressFamily, halibutTimeoutsAndLimits);
        }
        
        internal static TcpClient CreateTcpClientAsync(AddressFamily addressFamily, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            var client = new TcpClient(addressFamily)
            {
                SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.SendTimeout.TotalMilliseconds,
                ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout.TotalMilliseconds
            };

            if (client.Client.AddressFamily == AddressFamily.InterNetworkV6)
            {
                client.Client.DualMode = true;
            }
            return client;
        }
    }
}
