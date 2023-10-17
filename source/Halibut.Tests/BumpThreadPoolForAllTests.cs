using System;
using System.Threading;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BumpThreadPoolForAllTests
    {
        public BumpThreadPoolForAllTests()
        {
            var minWorkerPoolThreads = 400;
            ThreadPool.GetMinThreads(out _, out var minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            ThreadPool.SetMaxThreads(Math.Max(minWorkerPoolThreads, maxWorkerThreads), Math.Max(minCompletionPortThreads, maxCompletionPortThreads));
            ThreadPool.SetMinThreads(minWorkerPoolThreads, minCompletionPortThreads);
        }

        [SetUpFixture]
        [Obsolete("Remove when NUnit is fully replaced with xUnit")]
        public class TestsSetupClass
        {
            [OneTimeSetUp]
            [Obsolete("Remove when NUnit is fully replaced with xUnit")]
            public void GlobalSetup()
            {
                var minWorkerPoolThreads = 400;
                ThreadPool.GetMinThreads(out _, out var minCompletionPortThreads);
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                ThreadPool.SetMaxThreads(Math.Max(minWorkerPoolThreads, maxWorkerThreads), Math.Max(minCompletionPortThreads, maxCompletionPortThreads));
                ThreadPool.SetMinThreads(minWorkerPoolThreads, minCompletionPortThreads);
            }
        }
    }
}