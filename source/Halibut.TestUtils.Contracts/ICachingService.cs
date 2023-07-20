using System;

namespace Halibut.TestUtils.Contracts
{
    /// <summary>
    /// Don't use this interface to resolve a client proxy within a test since it does not have the cache attributes.
    ///
    /// This should only be used as the implemented interface.
    /// </summary>
    public interface ICachingService
    {
        Guid NonCachableCall();
        Guid CachableCall();
        Guid AnotherCachableCall();
        Guid CachableCall(Guid input);
        Guid CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(string exceptionMessagePrefix);
        Guid TwoSecondCachableCall();
    }
}