using System;

namespace Halibut.Tests.TestServices
{
    public class CachingService : ICachingService
    {
        public Guid NonCachableCall()
        {
            return Guid.NewGuid();
        }

        public Guid CachableCall()
        {
            return Guid.NewGuid();
        }

        public Guid AnotherCachableCall()
        {
            return Guid.NewGuid();
        }

        public Guid CachableCall(Guid input)
        {
            return Guid.NewGuid();
        }

        public Guid CachableCallThatThrowsAnException(string exceptionMessage)
        {
            throw new Exception(exceptionMessage + " " + Guid.NewGuid());
        }

        public Guid TwoSecondCachableCall()
        {
            return Guid.NewGuid();
        }
    }
}