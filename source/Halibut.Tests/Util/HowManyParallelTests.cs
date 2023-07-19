using System;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class HowManyParallelTests : BaseTest
    {
        [Test]
        public void HowManyTestsAreRunningInParallel()
        {
            // Only exists to make it easy to find out how many tests are running in parallel.
            Logger.Information("The number of parallel tests are: {LevelOfParallelism} on a machine with {NumberOfCpuCores} cpu cores.", 
                CustomLevelOfParallelismAttribute.LevelOfParallelism(),
                Environment.ProcessorCount);
        }
    }
}