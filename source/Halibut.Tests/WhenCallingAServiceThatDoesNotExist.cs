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
    public class WhenCallingAServiceThatDoesNotExist
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = await LatestClientAndLatestServiceBuilder
                       .ForServiceConnectionType(serviceConnectionType)
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
            }
        }
    }
}
