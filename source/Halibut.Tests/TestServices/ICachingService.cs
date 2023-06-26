using System;
using Halibut.Transport.Caching;

namespace Halibut.Tests.TestServices
{
    public interface ICachingService
    {
        Guid NonCachableCall();

        [CacheResponse(600)]
        Guid CachableCall();

        [CacheResponse(600)]
        Guid AnotherCachableCall();

        [CacheResponse(600)]
        Guid CachableCall(Guid input);

        [CacheResponse(600)]
        Guid CachableCallThatThrowsAnException(string exceptionMessage);

        [CacheResponse(2)]
        Guid TwoSecondCachableCall();
    }
}