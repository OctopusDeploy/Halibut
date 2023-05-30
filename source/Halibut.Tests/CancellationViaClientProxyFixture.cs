using System;
using System.Threading;
using FluentAssertions;
using Halibut.ServiceModel;
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