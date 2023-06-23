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
                ThreadPool.SetMaxThreads(Int32.MaxValue, Int32.MaxValue);
                ThreadPool.SetMinThreads(4000, 4000);
            }
        }
    }
}