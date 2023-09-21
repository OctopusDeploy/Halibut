using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public static class ParallelRequestsFixture
    {
        [Parallelizable(ParallelScope.None)]
        public class AsyncParallelRequestFixture : BaseTest
        {
            [Test]
            [LatestAndPreviousClientAndServiceVersionsTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false, testAsyncAndSyncClients: false)]
            public async Task MultipleRequestsCanBeInFlightInParallelAsync(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var builder = clientAndServiceTestCase.CreateTestCaseBuilder().WithStandardServices();
                if (!clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient())
                {
                    builder = builder.WithForcingClientProxyType(ForceClientProxyType.AsyncClient);
                }
                await using var clientAndService = await builder.Build(CancellationToken);

                var lockService = clientAndService.CreateAsyncClient<ILockService, IAsyncClientLockService>();

                var threadCount = NumberOfParallelRequests(clientAndServiceTestCase);
                long threadCompletionCount = 0;
                var threads = new List<Task>();

                var lockFile = Path.GetTempFileName();
                var requestStartedFilePathBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                var requestStartedFilePaths = new ConcurrentBag<string>();
                Logger.Information($"Lock file: {lockFile}");
                Logger.Information($"Request started files: {requestStartedFilePathBase}");

                for (var i = 0; i < threadCount; i++)
                {
                    int iteration = i;
                    var thread = Task.Run(async () =>
                        {
                            // Gotta handle exceptions when running in a 
                            // Thread, or you're gonna have a bad time.

                            var requestStartedPath = $"{requestStartedFilePathBase}-{iteration}";
                            requestStartedFilePaths.Add(requestStartedPath);
                            await lockService.WaitForFileToBeDeletedAsync(lockFile, requestStartedPath);
                            Interlocked.Increment(ref threadCompletionCount);
                        }
                    );
                    threads.Add(thread);
                }

                // Wait for all requests to be started
                await Wait.For(() => Task.FromResult(requestStartedFilePaths.All(File.Exists)), CancellationToken);

                Interlocked.Read(ref threadCompletionCount).Should().Be(0);

                // Let the remote calls complete
                File.Delete(lockFile);

                await Task.WhenAll(threads);

                Interlocked.Read(ref threadCompletionCount).Should().Be(threadCount);
            }

            [Test]
            [LatestAndPreviousClientAndServiceVersionsTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false, testAsyncAndSyncClients: false)]
            public async Task SendMessagesToTentacleInParallelAsync(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                var builder = clientAndServiceTestCase.CreateTestCaseBuilder();
                if (!clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient())
                {
                    builder = builder.WithForcingClientProxyType(ForceClientProxyType.AsyncClient);
                }
                
                await using var clientAndService = await builder
                    .WithStandardServices()
                    .Build(CancellationToken);

                var readDataSteamService = clientAndService.CreateClient<IReadDataStreamService, IAsyncReadDataStreamService>();

                var dataStreams = SyncParallelRequestsFixture.CreateDataStreams();

                var messagesAreSentTheSameTimeSemaphore = new SemaphoreSlim(0, dataStreams.Length);

                var threadCount = NumberOfParallelRequests(clientAndServiceTestCase);
                int threadCompletionCount = 0;
                var threads = new List<Task>();
                for (var i = 0; i < threadCount; i++)
                {
                    var thread = Task.Run(async () =>
                    {
                        // Gotta handle exceptions when running in a 
                        // Thread, or you're gonna have a bad time.
                        await messagesAreSentTheSameTimeSemaphore.WaitAsync(CancellationToken);
                        var received = await readDataSteamService.SendDataAsync(dataStreams);
                        received.Should().Be(5 * dataStreams.Length);
                        Interlocked.Increment(ref threadCompletionCount);
                        
                    });
                    threads.Add(thread);
                }

                messagesAreSentTheSameTimeSemaphore.Release(dataStreams.Length);

                await Task.WhenAll(threads);
                
                threadCompletionCount.Should().Be(threadCount);
            }
        }

        [Parallelizable(ParallelScope.None)]
        public class SyncParallelRequestsFixture : BaseTest
        {
            [Test]
            [LatestAndPreviousClientAndServiceVersionsTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false)]
            public async Task MultipleRequestsCanBeInFlightInParallel(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                    .WithStandardServices()
                    .Build(CancellationToken);

                var lockService = clientAndService.CreateClient<ILockService, IAsyncClientLockService>();

                var threadCount = NumberOfParallelRequests(clientAndServiceTestCase);
                long threadCompletionCount = 0;
                var threads = new List<Thread>();
                var exceptions = new ConcurrentBag<Exception>();

                var lockFile = Path.GetTempFileName();
                var requestStartedFilePathBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                var requestStartedFilePaths = new ConcurrentBag<string>();
                Logger.Information($"Lock file: {lockFile}");
                Logger.Information($"Request started files: {requestStartedFilePathBase}");

                for (var i = 0; i < threadCount; i++)
                {
                    int iteration = i;
                    var thread = new Thread(() =>
                    {
                        // Gotta handle exceptions when running in a 
                        // Thread, or you're gonna have a bad time.
                        try
                        {
                            var requestStartedPath = $"{requestStartedFilePathBase}-{iteration}";
                            requestStartedFilePaths.Add(requestStartedPath);
                            lockService.WaitForFileToBeDeletedAsync(lockFile, requestStartedPath).GetAwaiter().GetResult();
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

                // Wait for all requests to be started
                await Wait.For(() => Task.FromResult(requestStartedFilePaths.All(File.Exists)), CancellationToken);

                Interlocked.Read(ref threadCompletionCount).Should().Be(0);
                exceptions.Should().BeEmpty();

                // Let the remote calls complete
                File.Delete(lockFile);

                WaitForAllThreads(threads);

                Interlocked.Read(ref threadCompletionCount).Should().Be(threadCount);
                exceptions.Should().BeEmpty();
            }

            [Test]
            [LatestAndPreviousClientAndServiceVersionsTestCases(testPolling: false, testWebSocket: false, testNetworkConditions: false)]
            public async Task SendMessagesToTentacleInParallel(ClientAndServiceTestCase clientAndServiceTestCase)
            {
                await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                    .WithStandardServices()
                    .Build(CancellationToken);

                var readDataSteamService = clientAndService.CreateClient<IReadDataStreamService, IAsyncReadDataStreamService>();

                var dataStreams = CreateDataStreams();

                var messagesAreSentTheSameTimeSemaphore = new SemaphoreSlim(0, dataStreams.Length);

                var threadCount = NumberOfParallelRequests(clientAndServiceTestCase);
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
                            var received = readDataSteamService.SendDataAsync(dataStreams).GetAwaiter().GetResult();
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

            public static DataStream[] CreateDataStreams()
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
        
        static int NumberOfParallelRequests(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            if (clientAndServiceTestCase.ClientAndServiceTestVersion.IsPreviousClient())
            {
                // Reduce this down to reduce thread pool exhaustion.
                // We already test latest => latest with 64 concurrent task
                // So it is likely that an external old client will have no problems running
                // 64 concurrent task on a latest service.
                return 4;
            }

            return 32;
        }
    }
}
