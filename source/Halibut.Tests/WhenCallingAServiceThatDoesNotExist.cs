using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAServiceThatDoesNotExist : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
            }
        }
    }
}
