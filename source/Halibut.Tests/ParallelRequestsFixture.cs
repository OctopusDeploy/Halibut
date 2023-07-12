using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ParallelRequestsFixture
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task SendMessagesToTentacleInParallel(ServiceConnectionType serviceConnectionType)
        {
            var services = new DelegateServiceFactory();
            services.Register<IReadDataStreamService>(() => new ReadDataStreamService());

            using (var clientAndService = await ClientServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .WithServiceFactory(services).Build())
            {
                var readDataSteamService = clientAndService.CreateClient<IReadDataStreamService>();

                var dataStreams = CreateDataStreams();

                var messagesAreSentTheSameTimeSemaphore = new Semaphore(0, dataStreams.Length);

                var threads = new List<Thread>();
                for (var i = 0; i < 64; i++)
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
            var dataStreams = new DataStream[128];
            for (var i = 0; i < dataStreams.Length; i++)
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