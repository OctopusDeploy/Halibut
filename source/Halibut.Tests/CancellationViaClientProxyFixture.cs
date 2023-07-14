using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestAttributes;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class CancellationViaClientProxyFixture : BaseTest
    {
        [Test]
        public async Task CancellationCanBeDoneViaClientProxy()
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .NoService()
                       .WithEchoService()
                       .Build(CancellationToken))
            {
                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClient<IEchoService, IClientEchoService>(point =>
                    {
                        point.RetryCountLimit = 1000000;
                        point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                    });

                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));
                var func = new Func<string>(() => echo.SayHello("hello", new HalibutProxyRequestOptions(cts.Token)));
                var ex = Assert.Throws<HalibutClientException>(() => echo.SayHello("hello", new HalibutProxyRequestOptions(cts.Token)));
                ex.Message.Should().ContainAny("The operation was canceled");
            }
        }

        [Test]
        public async Task CannotHaveServiceWithHalibutProxyRequestOptions()
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder.Listening()
                       .NoService()
                       .WithService<IAmNotAllowed>(() => new AmNotAllowed())
                       .Build(CancellationToken))
            {
                Assert.Throws<TypeNotAllowedException>(() => clientAndService.CreateClient<IAmNotAllowed>());
            }
        }

        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task CanTalkToOldServicesWhichDontKnowAboutHalibutProxyRequestOptions(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndPreviousServiceVersionBuilder.ForServiceConnectionType(serviceConnectionType).WithServiceVersion("5.0.429").WithStandardServices().Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IClientEchoService>(se =>
                    {
                        se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                        se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                    });

                var res = echo.SayHello("Hello!!", new HalibutProxyRequestOptions(new CancellationToken()));
                res.Should().Be("Hello!!...");
            }
        }

        public interface IClientEchoService
        {
            int LongRunningOperation(HalibutProxyRequestOptions halibutProxyRequestOptions);

            string SayHello(string name, HalibutProxyRequestOptions halibutProxyRequestOptions);

            bool Crash(HalibutProxyRequestOptions halibutProxyRequestOptions);

            int CountBytes(DataStream stream, HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
    }

    public interface IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts);
    }

    public class AmNotAllowed : IAmNotAllowed
    {
        public void Foo(HalibutProxyRequestOptions opts)
        {
            throw new NotImplementedException();
        }
    }
}
