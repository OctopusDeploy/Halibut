using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class WhenCallingAServiceThatDoesNotExist : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false, testAsyncServicesAsWell: true)]
        public async Task AServiceNotFoundHalibutClientExceptionShouldBeRaisedByTheClient(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .Build(CancellationToken))
            {
                var echo = clientAndService.CreateClient<IEchoService, IAsyncClientEchoService>();
                Func<Task<string>> readAsyncCall = async () => await echo.SayHelloAsync("Say hello to a service that does not exist.");

                await readAsyncCall.Should().ThrowAsync<ServiceNotFoundHalibutClientException>();
            }
        }
    }
}
