using System;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;

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
            NonBlockingConsole.WriteLine("Forwarding NonCachableCall() call to delegate");
            return cachingService.NonCachableCall();
        }

        public Guid CachableCall()
        {
            NonBlockingConsole.WriteLine("Forwarding CachableCall() call to delegate");
            return cachingService.CachableCall();
        }

        public Guid CachableCall(Guid input)
        {
            NonBlockingConsole.WriteLine("Forwarding CachableCall(Guid) call to delegate");
            return cachingService.CachableCall(input);
        }

        public Guid AnotherCachableCall()
        {
            NonBlockingConsole.WriteLine("Forwarding AnotherCachableCall() call to delegate");
            return cachingService.AnotherCachableCall();
        }

        public Guid CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(string exceptionMessagePrefix)
        {
            NonBlockingConsole.WriteLine("Forwarding CachableCallThatThrowsAnExceptionWithARandomExceptionMessage() call to delegate");
            return cachingService.CachableCallThatThrowsAnExceptionWithARandomExceptionMessage(exceptionMessagePrefix);
        }

        public Guid TwoSecondCachableCall()
        {
            NonBlockingConsole.WriteLine("Forwarding TwoSecondCachableCall() call to delegate");
            return cachingService.TwoSecondCachableCall();
        }
    }
}