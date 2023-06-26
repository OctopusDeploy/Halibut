using System;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public interface ICachingService
    {
        Guid NonCachableCall();

        // The CacheResponse Attribute cannot be applied / isn't need for the Service interface
        // It is applied on the client interface in the Halibut.Tests project
        Guid CachableCall();

        // The CacheResponse Attribute cannot be applied / isn't need for the Service interface
        // It is applied on the client interface in the Halibut.Tests project
        Guid CachableCall(Guid input);

        // The CacheResponse Attribute cannot be applied / isn't need for the Service interface
        // It is applied on the client interface in the Halibut.Tests project
        Guid AnotherCachableCall();

        // The CacheResponse Attribute cannot be applied / isn't need for the Service interface
        // It is applied on the client interface in the Halibut.Tests project
        Guid CachableCallThatThrowsAnException(string exceptionMessage);

        // The CacheResponse Attribute cannot be applied / isn't need for the Service interface
        // It is applied on the client interface in the Halibut.Tests project
        Guid TwoSecondCachableCall();
    }
}