using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Transport.Caching;
using NUnit.Framework;
using ICachingService = Halibut.Tests.TestServices.ICachingService;

namespace Halibut.Tests
{
    public class ResponseMessageCacheFixture : BaseTest
    {
        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatDoesNotSupportCaching_ResponsesShouldNotBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.NonCachableCall();
                var result2 = client.NonCachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatDoesNotSupportCaching_WithClientInterface_ResponsesShouldNotBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IClientCachingService>();

                var result1 = client.NonCachableCall(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = client.NonCachableCall(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall();
                var result2 = client.CachableCall();

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_WithClientInterface_ResponseShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService, IClientCachingService>();

                var result1 = client.CachableCall(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = client.CachableCall(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ResponseForServiceWithInputParametersShouldBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var guid = Guid.NewGuid();
                var result1 = client.CachableCall(guid);
                var result2 = client.CachableCall(guid);

                result1.Should().Be(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_CachedItemShouldBeInvalidatedAfterTheCacheDurationExpires(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.TwoSecondCachableCall();
                var result2 = client.TwoSecondCachableCall();

                result1.Should().Be(result2);

                await Task.Delay(TimeSpan.FromSeconds(3));

                var result3 = client.TwoSecondCachableCall();

                result3.Should().NotBe(result1);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentServices(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall();
                var result2 = client.AnotherCachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentEndpoints(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndServiceOne = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var clientOne = clientAndServiceOne.CreateClient<ICachingService>();

                using var clientAndServiceTwo = await clientAndServiceTestCase.CreateTestCaseBuilder()
                           .WithCachingService()
                           .WithHalibutLoggingLevel(LogLevel.Info)
                           .Build(CancellationToken);

                    var clientTwo = clientAndServiceTwo.CreateClient<ICachingService>();

                var result1 = clientOne.CachableCall();
                var result2 = clientTwo.CachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentInputParameters(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall(Guid.NewGuid());
                var result2 = client.CachableCall(Guid.NewGuid());

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ClientShouldBeAbleToForceSpecificErrorResponsesToBeCached(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                clientAndService.Client.OverrideErrorResponseMessageCaching = response => response.Error.Message.Contains("CACHE ME");

                var client = clientAndService.CreateClient<ICachingService>();

                var exception1 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"UNCACHED"));
                var exception2 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"UNCACHED"));
                exception1!.Message.Should().NotBe(exception2!.Message);


                var exception3 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"CACHE ME"));
                var exception4 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"CACHE ME"));
                exception3!.Message.Should().Be(exception4!.Message);
            }
        }

        [Test]
        [LatestClientAndLatestAndPreviousServiceVersionsTestCases(testNetworkConditions: false)]
        [FailedWebSocketTestsBecomeInconclusive]
        public async Task ForAServiceThatSupportsCaching_ErrorResponsesShouldNotBeCachedByDefault(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithCachingService()
                       .WithHalibutLoggingLevel(LogLevel.Info)
                       .Build(CancellationToken))
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var exception1 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"Exception"));
                var exception2 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"Exception"));

                exception1!.Message.Should().StartWith("Exception");
                exception2!.Message.Should().StartWith("Exception");
                exception1!.Message.Should().NotBe(exception2.Message);
            }
        }
        
        public interface IClientCachingService
        {
            Guid NonCachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);

            [CacheResponse(600)]
            Guid CachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
    }
}
