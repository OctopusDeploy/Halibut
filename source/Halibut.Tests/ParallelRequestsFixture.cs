using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ParallelRequestsFixture
    {
        [Test]
        public void SendMessagesToAListeningTentacleInParallel()
        {
            var services = new DelegateServiceFactory();
            services.Register<IReadDataSteamService>(() => new ReadDataStreamService());
            
            using (var clientAndService = ClientServiceBuilder.Listening().WithServiceFactory(services).Build())
            {
                var readDataSteamService = clientAndService.CreateClient<IReadDataSteamService>();

                var dataStreams = CreateDataStreams();

                Semaphore messagesAreSentTheSameTimeSemaphore = new Semaphore(0, dataStreams.Length);

                var threads = new List<Thread>();
                for (int i = 0; i < 64; i++)
                {
                    var thread = new Thread(() =>
                    {
                        messagesAreSentTheSameTimeSemaphore.WaitOne();
                        var recieved = readDataSteamService.SendData(dataStreams);
                        recieved.Should().Be(5 * dataStreams.Length);
                    });
                    thread.Start();
                    threads.Add(thread);
                }

                messagesAreSentTheSameTimeSemaphore.Release(dataStreams.Length);

                WaitForAllThreads(threads);
            }
        }

        static DataStream[] CreateDataStreams()
        {
            // Lots of DataStreams since they are handled in a special way, and we have had threading issues
            // with these previously.
            DataStream[] dataStreams = new DataStream[128];
            for (int i = 0; i < dataStreams.Length; i++)
            {
                dataStreams[i] = DataStream.FromString("Hello");
            }

            return dataStreams;
        }
        
        static void WaitForAllThreads(List<Thread> threads)
        {
            foreach (var thread in threads)
            {
                thread.Join();
            }
        }
    }
}