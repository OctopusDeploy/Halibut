using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class CancellationViaClientProxyFixture
    {
        [Test]
        public void CancellationCanBeDoneViaClientProxy()
        {
            using (var clientAndService = ClientServiceBuilder.Listening()
                       .NoService()
                       .WithService(new EchoService())
                       .Build())
            {
                var data = new byte[1024 * 1024 + 15];
                new Random().NextBytes(data);

                var echo = clientAndService.CreateClient<IEchoService, IClientEchoService>(point =>
                {
                    point.RetryCountLimit = 1000000;
                    point.ConnectionErrorRetryTimeout = TimeSpan.MaxValue;
                },
                    CancellationToken.None);

                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));
                var func = new Func<string>(() => echo.SayHello("hello", new HalibutProxyRequestOptions(cts.Token)));
                var ex = Assert.Throws<Halibut.HalibutClientException>(() => echo.SayHello("hello", new HalibutProxyRequestOptions(cts.Token)));
                ex.Message.Should().ContainAny("The operation was canceled");
            }
        }
        
        [Test]
        public void CannotHaveServiceWithHalibutProxyRequestOptions()
        {
            using (var clientAndService = ClientServiceBuilder.Listening()
                       .NoService()
                       .WithService(new AmNotAllowed())
                       .Build())
            {

                Assert.Throws<TypeNotAllowedException>(() => clientAndService.CreateClient<IAmNotAllowed>());
            }
        }

        [Test]
        [TestCase(ServiceConnectionType.Listening)]
        [TestCase(ServiceConnectionType.Polling)]
        public async Task CanTalkToOldServicesWhichDontKnowAboutHalibutProxyRequestOptions(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await ClientAndPreviousVersionServiceBuilder.WithService(serviceConnectionType).WithServiceVersion("5.0.429").Build())
            {
                var echo = clientAndService.CreateClient<IEchoService, IClientEchoService>(se =>
                {
                    se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(20);
                    se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(20);
                },
                    CancellationToken.None);

                var res = echo.SayHello("Hello!!", new HalibutProxyRequestOptions(new CancellationToken()));
                res.Should().Be("Hello!!");
            }
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

    public interface IClientEchoService
    {
        int LongRunningOperation(HalibutProxyRequestOptions halibutProxyRequestOptions);

        string SayHello(string name, HalibutProxyRequestOptions halibutProxyRequestOptions);

        bool Crash(HalibutProxyRequestOptions halibutProxyRequestOptions);

        int CountBytes(DataStream stream, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}