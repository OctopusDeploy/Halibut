using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAServiceThatDoesNotExist
    {
        [Test]
        [TestCaseSource(typeof(ServiceConnectionTypesToTest))]
        public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ServiceConnectionType serviceConnectionType)
        {
            using (var clientAndService = ClientServiceBuilder
                       .ForMode(serviceConnectionType)
                       .Build())
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
            }
        }
    }
}