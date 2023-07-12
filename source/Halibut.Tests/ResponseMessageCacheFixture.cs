using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Caching;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class ResponseMessageCacheFixture
    {
        public static object[] ServiceConnectionTypeAndVersion =
        {
            new object[] { ServiceConnectionType.Polling, null },
            new object[] { ServiceConnectionType.Listening, null },
            new object[] { ServiceConnectionType.Polling, PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417 },
            new object[] { ServiceConnectionType.Listening, PreviousVersions.v5_0_236_Used_In_Tentacle_6_3_417 }
        };

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatDoesNotSupportCaching_ResponsesShouldNotBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.NonCachableCall();
                var result2 = client.NonCachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatDoesNotSupportCaching_WithClientInterface_ResponsesShouldNotBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService, IClientCachingService>();

                var result1 = client.NonCachableCall(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = client.NonCachableCall(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall();
                var result2 = client.CachableCall();

                result1.Should().Be(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_WithClientInterface_ResponseShouldBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService, IClientCachingService>();

                var result1 = client.CachableCall(new HalibutProxyRequestOptions(CancellationToken.None));
                var result2 = client.CachableCall(new HalibutProxyRequestOptions(CancellationToken.None));

                result1.Should().Be(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ResponseForServiceWithInputParametersShouldBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var guid = Guid.NewGuid();
                var result1 = client.CachableCall(guid);
                var result2 = client.CachableCall(guid);

                result1.Should().Be(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_CachedItemShouldBeInvalidatedAfterTheCacheDurationExpires(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
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
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentServices(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall();
                var result2 = client.AnotherCachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentEndpoints(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndServiceOne = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var clientOne = clientAndServiceOne.CreateClient<ICachingService>();

                using var clientAndServiceTwo = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
                var clientTwo = clientAndServiceTwo.CreateClient<ICachingService>();

                var result1 = clientOne.CachableCall();
                var result2 = clientTwo.CachableCall();

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ResponseShouldBeDifferentForDifferentInputParameters(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var result1 = client.CachableCall(Guid.NewGuid());
                var result2 = client.CachableCall(Guid.NewGuid());

                result1.Should().NotBe(result2);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ClientShouldBeAbleToForceSpecificErrorResponsesToBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                clientAndService.Octopus.OverrideErrorResponseMessageCaching = response => response.Error.Message.Contains("CACHE ME");

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
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ErrorResponsesShouldNotBeCachedByDefault(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();

                var exception1 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"Exception"));
                var exception2 = Assert.Throws<ServiceInvocationHalibutClientException>(() => client.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage($"Exception"));

                exception1!.Message.Should().StartWith("Exception");
                exception2!.Message.Should().StartWith("Exception");
                exception1!.Message.Should().NotBe(exception2.Message);
            }
        }

        static async Task<IClientAndService> CreateClientAndService(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            return halibutServiceVersion == null ?
                await ClientServiceBuilder
                    .ForServiceConnectionType(serviceConnectionType)
                    .WithCachingService()
                    .Build() :
                await ClientAndPreviousServiceVersionBuilder
                    .ForServiceConnectionType(serviceConnectionType)
                    .WithServiceVersion(halibutServiceVersion)
                    .Build();
        }

        public interface IClientCachingService
        {
            Guid NonCachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);

            [CacheResponse(600)]
            Guid CachableCall(HalibutProxyRequestOptions halibutProxyRequestOptions);
        }
    }
}
