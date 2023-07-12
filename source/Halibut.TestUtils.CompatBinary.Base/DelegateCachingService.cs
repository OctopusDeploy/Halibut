using System;
using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateCachingService : ICachingService
    {
        readonly ICachingService cachingService;

        public DelegateCachingService(ICachingService cachingService)
        {
            this.cachingService = cachingService;
        }

        public Guid NonCachableCall()
        {
            Console.WriteLine("Forwarding NonCachableCall() call to delegate");
            return cachingService.NonCachableCall();
        }

        public Guid CachableCall()
        {
            Console.WriteLine("Forwarding CachableCall() call to delegate");
            return cachingService.CachableCall();
        }

        public Guid CachableCall(Guid input)
        {
            Console.WriteLine("Forwarding CachableCall(Guid) call to delegate");
            return cachingService.CachableCall(input);
        }

        public Guid AnotherCachableCall()
        {
            Console.WriteLine("Forwarding AnotherCachableCall() call to delegate");
            return cachingService.AnotherCachableCall();
        }

        public Guid CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(string exceptionMessagePrefix)
        {
            Console.WriteLine("Forwarding CachableCallThatThrowsAnExceptionWithARandomExceptionMessage() call to delegate");
            return cachingService.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(exceptionMessagePrefix);
        }

        public Guid TwoSecondCachableCall()
        {
            Console.WriteLine("Forwarding TwoSecondCachableCall() call to delegate");
            return cachingService.TwoSecondCachableCall();
        }
    }
}