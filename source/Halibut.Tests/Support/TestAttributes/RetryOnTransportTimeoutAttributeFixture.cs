using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    public class RetryOnTransportTimeoutAttributeFixture : BaseTest
    {
        static readonly ConcurrentDictionary<string, int> Attempts = new();

        static int NextAttempt(string name) => Attempts.AddOrUpdate(name, 1, (_, n) => n + 1);

        [Test]
        [RetryOnTransportTimeout(tryCount: 3, netFrameworkOnly: false)]
        public async Task ATestThatTimesOutOnceThenPasses_IsRetriedAndPasses()
        {
            await Task.Yield();

            var attempt = NextAttempt(nameof(ATestThatTimesOutOnceThenPasses_IsRetriedAndPasses));
            if (attempt == 1)
            {
                throw new HalibutClientException("An error occurred when sending a request",
                    new IOException("Unable to read data from the transport connection",
                        new SocketException((int)SocketError.TimedOut)));
            }

            attempt.Should().Be(2, "the first attempt failed with a transport timeout and should be retried exactly once");
        }

        [Test]
        [RetryOnTransportTimeout(tryCount: 3, netFrameworkOnly: false)]
        public void SetUpRunsAgainForEachAttempt()
        {
            var attempt = NextAttempt(nameof(SetUpRunsAgainForEachAttempt));

            CancellationToken.IsCancellationRequested.Should().BeFalse("BaseTest.SetUp should give each attempt a fresh token");
            if (attempt == 1) throw new SocketException((int)SocketError.TimedOut);
        }
    }
}
