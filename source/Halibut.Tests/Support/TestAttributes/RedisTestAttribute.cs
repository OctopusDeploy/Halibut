using System;
using Halibut.Tests.TestSetup.Redis;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RedisTestAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
        {
            return;
        }

        if (!EnsureRedisIsAvailableSetupFixture.WillRunRedisTests)
        {
            test.RunState = RunState.Skipped;
            test.Properties.Add("_SKIPREASON", "Redis tests are not yet supported on this OS or dotnet version.");
        }
    }
}
