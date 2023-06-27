using System;

namespace Halibut.TestUtils.SampleProgram.Base
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

        public Guid CachableCall(Guid input)
        {
            return Guid.NewGuid();
        }

        public Guid AnotherCachableCall()
        {
            return Guid.NewGuid();
        }

        public Guid CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(string exceptionMessagePrefix)
        {
            throw new Exception(exceptionMessagePrefix + " " + Guid.NewGuid());
        }

        public Guid TwoSecondCachableCall()
        {
            return Guid.NewGuid();
        }
    }
}