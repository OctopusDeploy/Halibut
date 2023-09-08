using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
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
        }

        [Test]
        [WindowsTest]
        [SyncAndAsync]
        public void SecureListenerDoesNotCreateHundredsOfIoEventsPerSecondOnWindows(SyncOrAsync syncOrAsync)
        {
            var logger = new SerilogLoggerBuilder().Build();
            const int secondsToSample = 5;

            using (var opsPerSec = GetCounterForCurrentProcess("Process", "IO Other Operations/sec"))
            {
                var client = new SecureListener(
                    new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 0),
                    Certificates.TentacleListening,
                    null,
                    null,
                    _ => true,
                    new LogFactory(),
                    () => "",
                    () => new Dictionary<string, string>(),
                    (_, _) => UnauthorizedClientConnectResponse.BlockConnection,
                    syncOrAsync.ToAsyncHalibutFeature(),
                    new HalibutTimeoutsAndLimits(),
                    new StreamFactory(syncOrAsync.ToAsyncHalibutFeature()),
                    NoOpConnectionsObserver.Instance
                );

                var idleAverage = CollectCounterValues(opsPerSec)
                    .Take(secondsToSample)
                    .Average();

                float listeningAverage;

                using (client)
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
                yield return counter.NextValue();
            }
            // ReSharper disable once IteratorNeverReturns : Take is limit
        }
    }
}
