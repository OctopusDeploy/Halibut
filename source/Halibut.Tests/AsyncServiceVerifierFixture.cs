using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class AsyncServiceVerifierFixture
    {
        [Test]
        public async Task AsyncMatchesSync()
        {
            AsyncServiceVerifier.VerifyAsyncSurfaceAreaFollowsConventions<IGoodService, IAsyncGoodService>();
        }

        [Test]
        public async Task AsyncBreaksCancellationTokenConvention()
        {
            Assert.Throws<NoMatchingServiceOrMethodHalibutClientException>
                (AsyncServiceVerifier.VerifyAsyncSurfaceAreaFollowsConventions<IGoodService, IAsyncBrokenCancellationTokenConventionService>);
        }

        [Test]
        public async Task AsyncBreaksSuffixConvention()
        {
            Assert.Throws<NoMatchingServiceOrMethodHalibutClientException>
                (AsyncServiceVerifier.VerifyAsyncSurfaceAreaFollowsConventions<IGoodService, IAsyncBrokenSuffixConventionService>);
        }

        [Test]
        public async Task AsyncBreaksReturnTypeConvention()
        {
            Assert.Throws<NoMatchingServiceOrMethodHalibutClientException>
                (AsyncServiceVerifier.VerifyAsyncSurfaceAreaFollowsConventions<IGoodService, IAsyncBrokenReturnTypeConventionService>);
        }
        
    }

    public interface IGoodService
    {
        int DoSomething();
        string SaySomething(string name);
    }

    public interface IAsyncGoodService
    {
        Task<int> DoSomethingAsync(CancellationToken cancellationToken);
        Task<string> SaySomethingAsync(string name, CancellationToken cancellationToken);
    }
    
    public interface IAsyncBrokenCancellationTokenConventionService
    {
        Task<int> DoSomethingAsync();
        Task<string> SaySomethingAsync(string name);
    }

    public interface IAsyncBrokenSuffixConventionService
    {
        Task<int> DoSomething(CancellationToken cancellationToken);
        Task<string> SaySomething(string name, CancellationToken cancellationToken);
    }

    public interface IAsyncBrokenReturnTypeConventionService
    {
        int DoSomethingAsync(CancellationToken cancellationToken);
        string SaySomethingAsync(string name, CancellationToken cancellationToken);
    }
}
