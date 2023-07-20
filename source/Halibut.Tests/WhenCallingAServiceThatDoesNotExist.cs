using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAServiceThatDoesNotExist : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService>();
                Func<string> readAsyncCall = () => echo.SayHello("Say hello to a service that does not exist.");

                readAsyncCall.Should().Throw<ServiceNotFoundHalibutClientException>();
            }
        }
    }
}
