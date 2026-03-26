using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests.Transport
{
    public class WhenTransportUsesFlushBufferedStreamFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task RequestsSucceed_WhenStreamsOnlyForwardDataOnFlush(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithClientStreamFactory(new StreamWrappingStreamFactory { WrapStreamWith = s => new FlushBufferedStream(s) })
                .WithServiceStreamFactory(new StreamWrappingStreamFactory { WrapStreamWith = s => new FlushBufferedStream(s) })
                .WithEchoService()
                .Build(CancellationToken);

            var echo = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

            for (var i = 0; i < clientAndServiceTestCase.RecommendedIterations; i++)
            {
                var result = await echo.SayHelloAsync("hello");
                result.Should().Be("hello...");
            }
        }
    }
}
