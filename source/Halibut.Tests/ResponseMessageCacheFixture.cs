using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.TestServices.SyncClientWithOptions;
using NUnit.Framework;
using ICachingService = Halibut.Tests.TestServices.ICachingService;

namespace Halibut.Tests
{
    public class ResponseMessageCacheFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatDoesNotSupportCaching_ResponsesShouldNotBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = await client.NonCachableCallAsync();
                var result2 = await client.NonCachableCallAsync();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatDoesNotSupportCaching_WithClientInterface_ResponsesShouldNotBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClientWithOptions<ICachingService, IClientCachingService, IAsyncClientCachingServiceWithOptions>();

                var result1 = await client.NonCachableCallAsync(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = await client.NonCachableCallAsync(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = await client.CachableCallAsync();
                var result2 = await client.CachableCallAsync();

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_WithClientInterface_ResponseShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClientWithOptions<ICachingService, IClientCachingService, IAsyncClientCachingServiceWithOptions>();

                var result1 = await client.CachableCallAsync(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = await client.CachableCallAsync(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseForServiceWithInputParametersShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var guid = Guid.NewGuid();
                var result1 = await client.CachableCallAsync(guid);
                var result2 = await client.CachableCallAsync(guid);

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_CachedItemShouldBeInvalidatedAfterTheCacheDurationExpires(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = await client.TwoSecondCachableCallAsync();
                var result2 = await client.TwoSecondCachableCallAsync();

                result1.Should().Be(result2);

                await Task.Delay(TimeSpan.FromSeconds(3));

                var result3 = await client.TwoSecondCachableCallAsync();

                result3.Should().NotBe(result1);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentServices(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = await client.CachableCallAsync();
                var result2 = await client.AnotherCachableCallAsync();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentEndpoints(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndServiceOne = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var clientOne = clientAndServiceOne.CreateClient<ICachingService, IAsyncClientCachingService>();

                using var clientAndServiceTwo = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithCachingService()
                           .Build(CancellationToken);

                    var clientTwo = clientAndServiceTwo.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = clientOne.CachableCallAsync();
                var result2 = clientTwo.CachableCallAsync();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentInputParameters(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var result1 = await client.CachableCallAsync(Guid.NewGuid());
                var result2 = await client.CachableCallAsync(Guid.NewGuid());

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ClientShouldBeAbleToForceSpecificErrorResponsesToBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                clientAndService.Client.OverrideErrorResponseMessageCaching = response => response.Error.Message.Contains("CACHE ME");

                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var exception1 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"UNCACHED"))).And;
                var exception2 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"UNCACHED"))).And;
                exception1!.Message.Should().NotBe(exception2!.Message);


                var exception3 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"CACHE ME"))).And;
                var exception4 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"CACHE ME"))).And;
                exception3!.Message.Should().Be(exception4!.Message);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ErrorResponsesShouldNotBeCachedByDefault(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IAsyncClientCachingService>();

                var exception1 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"Exception"))).And;
                var exception2 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"Exception"))).And;

                exception1!.Message.Should().StartWith("Exception");
                exception2!.Message.Should().StartWith("Exception");
                exception1!.Message.Should().NotBe(exception2.Message);
            }
        }
    }
}
