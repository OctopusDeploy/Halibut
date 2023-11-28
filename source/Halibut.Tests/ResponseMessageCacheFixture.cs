using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.TestServices.SyncClientWithOptions;
using Halibut.Transport.Caching;
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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingServiceWithOptions>();

                var result1 = await client.NonCachableCallAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None));
                var result2 = await client.NonCachableCallAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None));

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingServiceWithOptions>();

                var result1 = await client.CachableCallAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None));
                var result2 = await client.CachableCallAsync(new HalibutProxyRequestOptions(CancellationToken, CancellationToken.None));

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestServiceTestCases(testNetworkConditions: false)]
        [LatestClientAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        public async Task ForAServiceThatSupportsCaching_ResponseForServiceWithInputParametersShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndServiceOne = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var clientOne = clientAndServiceOne.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

                await using var clientAndServiceTwo = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithCachingService()
                           .Build(CancellationToken);

                    var clientTwo = clientAndServiceTwo.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                clientAndService.Client.OverrideErrorResponseMessageCaching = response => response.Error.Message.Contains("CACHE ME");

                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

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
            await using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateAsyncClient<ICachingService, IAsyncClientCachingService>();

                var exception1 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"Exception"))).And;
                var exception2 = (await AssertAsync.Throws<ServiceInvocationHalibutClientException>(async () => await client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync($"Exception"))).And;

                exception1!.Message.Should().StartWith("Exception");
                exception2!.Message.Should().StartWith("Exception");
                exception1!.Message.Should().NotBe(exception2.Message);
            }
        }

        [Test]
        public void NullRequestsAreNotCached()
        {
            var cache = new ResponseCache();
            var serviceEndpoint = new ServiceEndPointBuilder().Build();
            var request = new RequestMessageBuilder(serviceEndpoint.BaseUri.ToString()).Build();
            var methodInfo = typeof(IAsyncClientCachingService).GetMethods().First(m => m.Name.Equals("CachableCallAsync"));
            var response = new ResponseMessageBuilder(serviceEndpoint.BaseUri.ToString()).Build();
            OverrideErrorResponseMessageCachingAction action = message => false;
            
            // Actual test, testing a null response.
            cache.CacheResponse(serviceEndpoint, request, methodInfo, null, action);
            cache.GetCachedResponse(serviceEndpoint, request, methodInfo).Should().BeNull();
            
            // This just checks we are using the cache correctly, ensuring the above is valid.
            cache.CacheResponse(serviceEndpoint, request, methodInfo, response, action);
            cache.GetCachedResponse(serviceEndpoint, request, methodInfo).Should().NotBeNull();
        }
    }
}
