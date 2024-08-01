using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class HalibutRuntimeDisposal : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        public async Task CanDisposeOfHalibutClientAndService(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var timeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
            timeoutsAndLimits.PollingRequestQueueTimeout = TimeSpan.FromSeconds(15);

            var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .WithStandardServices()
                .AsLatestClientAndLatestServiceBuilder()
                .WithHalibutTimeoutsAndLimits(timeoutsAndLimits)
                .Build(CancellationToken);
            
            var echoServiceClient = clientAndService.CreateAsyncClient<IEchoService, IAsyncClientEchoService>();

            await echoServiceClient.SayHelloAsync("Hi");

#pragma warning disable VSTHRD103
#pragma warning disable CS0618 // Type or member is obsolete
            clientAndService.Service.Dispose();
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore VSTHRD103

            // The Service and Client are both HalibutRuntimes so ensuring that the Service is Dispose works should also ensure that the Client Dispose works.
            // This test does not test specifics of the Dispose to ensure that all resources are cleaned up, but rather that the HalibutRuntime stops working.
            // Future improvements could ensure that all the specifics of Dispose work as expected e.g. active connections are disconnected, listening ports freed up etc.
             await AssertionExtensions.Should(() => echoServiceClient.SayHelloAsync("Hey")).ThrowAsync<HalibutClientException>("The Service should have been shutdown on Dispose");

#pragma warning disable VSTHRD103
#pragma warning disable CS0618 // Type or member is obsolete
            clientAndService.Client.Dispose();
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore VSTHRD103
        }
    }
}