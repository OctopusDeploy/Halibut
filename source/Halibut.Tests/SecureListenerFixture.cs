using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class SecureListenerFixture
    {
#if NETFRAMEWORK
        [Test]
        [WindowsTest]
        public void SecureListenerDoesNotCreateHundredsOfIOEventsPerSecondOnWindows()
        {
            const int secondsToSample = 5;
            
            var currentProcess = Process.GetCurrentProcess().ProcessName;
            var opsPerSec = new PerformanceCounter("Process", "IO Other Operations/sec", currentProcess);

            var client = new SecureListener(
                new IPEndPoint(new IPAddress(new byte[]{ 127, 0, 0, 1 }), 1093), 
                Certificates.TentacleListening,
                p => { },
                thumbprint => true,
                new LogFactory(), 
                () => ""
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
            
            TestContext.Out.WriteLine($"idle average:      {idleAverage} ops/second");
            TestContext.Out.WriteLine($"listening average: {listeningAverage} ops/second");
            TestContext.Out.WriteLine($"expectation:     < {idleAverageWithErrorMargin} ops/second");

            listeningAverage.Should().BeLessThan(idleAverageWithErrorMargin);
        }
#endif
        
        IEnumerable<float> CollectCounterValues(PerformanceCounter counter)
        {
            var sleepTime = TimeSpan.FromSeconds(1);
            
            while (true)
            {
                Thread.Sleep(sleepTime);
                yield return counter.NextValue();
            }
        }
    }
}