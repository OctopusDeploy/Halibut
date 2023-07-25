using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ParallelRequestsFixture : BaseTest
    {
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testPolling:false, testWebSocket: false, testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task SendMessagesToTentacleInParallel(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .WithHalibutLoggingLevel(LogLevel.Info)
                .WithStandardServices()
                .Build(CancellationToken);
            {
                var readDataSteamService = clientAndService.CreateClient<IReadDataStreamService>();

                var dataStreams = CreateDataStreams();

                var messagesAreSentTheSameTimeSemaphore = new SemaphoreSlim(0, dataStreams.Length);

                int threadCount = 64;
                int threadCompletionCount = 0;
                var threads = new List<Thread>();
                var exceptions = new ConcurrentBag<Exception>();
                for (var i = 0; i < threadCount; i++)
                {
                    var thread = new Thread(() =>
                    {
                        // Gotta handle exceptions when running in a 
                        // Thread, or you're gonna have a bad time.
                        try
                        {
                            messagesAreSentTheSameTimeSemaphore.Wait(CancellationToken);
                            var received = readDataSteamService.SendData(dataStreams);
                            received.Should().Be(5 * dataStreams.Length);
                            Interlocked.Increment(ref threadCompletionCount);
                        }
                        catch (Exception e)
                        {
                            exceptions.Add(e);
                        }
                    });
                    thread.Start();
                    threads.Add(thread);
                }

                messagesAreSentTheSameTimeSemaphore.Release(dataStreams.Length);

                WaitForAllThreads(threads);
                exceptions.Should().BeEmpty();
                threadCompletionCount.Should().Be(threadCount);
            }
        }

        static DataStream[] CreateDataStreams()
        {
            // Lots of DataStreams since they are handled in a special way, and we have had threading issues
            // with these previously.
            var dataStreams = new DataStream[128];
            for (var i = 0; i < dataStreams.Length; i++)
            {
                dataStreams[i] = DataStream.FromString("Hello");
            }

            return dataStreams;
        }

        void WaitForAllThreads(List<Thread> threads)
        {
            foreach (var thread in threads)
            {
                thread.Join();
            }
        }
    }
}
