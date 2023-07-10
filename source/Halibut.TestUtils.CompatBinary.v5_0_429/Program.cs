using System;
using Halibut.Diagnostics;
using Halibut.TestUtils.SampleProgram.Base;

namespace Halibut.TestUtils.SampleProgram.v5_0_429
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var s = HalibutLimits.PollingRequestQueueTimeout;
            Console.WriteLine(s);
           return BackwardsCompatProgramBase.Main(args);
        }
    }
}