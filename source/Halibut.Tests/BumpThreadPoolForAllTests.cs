using System;
using System.Threading;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BumpThreadPoolForAllTests
    {
        [SetUpFixture]
        public class TestsSetupClass
        {
            [OneTimeSetUp]
            public void GlobalSetup()
            {
                var minWorkerPoolThreads = 5000;
                var minCompletionPortThreads = 5000;
                ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                ThreadPool.SetMaxThreads(Math.Max(minWorkerPoolThreads, maxWorkerThreads), Math.Max(minCompletionPortThreads, maxCompletionPortThreads));
                ThreadPool.SetMinThreads(minWorkerPoolThreads, minCompletionPortThreads);
            }
        }
    }
}