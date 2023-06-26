using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.BackwardsCompatibility.Util;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
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
            new object[] { ServiceConnectionType.Polling, PreviousServiceVersions.v5_0_237_Used_In_Tentacle_6_3_417 },
            new object[] { ServiceConnectionType.Listening, PreviousServiceVersions.v5_0_237_Used_In_Tentacle_6_3_417 }
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
                clientAndService.Octopus.OverrideErrorResponseMessageCaching = (response) =>
                {
                    if (response.Error.Message.Contains("CACHE ME"))
                    {
                        return true;
                    }

                    return false;
                };

                var client = clientAndService.CreateClient<ICachingService>();

                Exception exception1 = null;
                Exception exception2 = null;

                TryCatch(() => client.CachableCallThatThrowsAnException($"UNCACHED"), e => exception1 = e);
                TryCatch(() => client.CachableCallThatThrowsAnException($"UNCACHED"), e => exception2 = e);

                exception1.Should().NotBeNull();
                exception2.Should().NotBeNull();
                exception1!.Message.Should().NotBe(exception2!.Message);


                Exception exception3 = null;
                Exception exception4 = null;

                TryCatch(() => client.CachableCallThatThrowsAnException($"CACHE ME"), e => exception3 = e);
                TryCatch(() => client.CachableCallThatThrowsAnException($"CACHE ME"), e => exception4 = e);

                exception3.Should().NotBeNull();
                exception4.Should().NotBeNull();
                exception3!.Message.Should().Be(exception4!.Message);
            }
        }

        [Test]
        [TestCaseSource(nameof(ServiceConnectionTypeAndVersion))]
        public async Task ForAServiceThatSupportsCaching_ErrorResponsesShouldNotBeCached(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            using var clientAndService = await CreateClientAndService(serviceConnectionType, halibutServiceVersion);
            {
                var client = clientAndService.CreateClient<ICachingService>();
                Exception exception1 = null;
                Exception exception2 = null;

                TryCatch(() => client.CachableCallThatThrowsAnException($"Exception"), e => exception1 = e);
                TryCatch(() => client.CachableCallThatThrowsAnException($"Exception"), e => exception2 = e);

                exception1.Should().NotBeNull();
                exception1!.Message.Should().StartWith("Exception");
                exception2.Should().NotBeNull();
                exception2!.Message.Should().StartWith("Exception");
                exception1!.Message.Should().NotBe(exception2.Message);
            }
        }

        static async Task<IClientAndService> CreateClientAndService(ServiceConnectionType serviceConnectionType, string halibutServiceVersion)
        {
            return halibutServiceVersion == null ?
                ClientServiceBuilder
                    .ForMode(serviceConnectionType)
                    .WithService<ICachingService>(() => new CachingService())
                    .Build() :
                await ClientAndPreviousServiceVersionBuilder
                    .ForServiceConnectionType(serviceConnectionType)
                    .WithServiceVersion(halibutServiceVersion)
                    .Build();
        }

        void TryCatch(Action action, Action<Exception> handleException)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                handleException(e);
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