using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using OSPlatform = System.Runtime.InteropServices.OSPlatform;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class WindowsTestAttribute : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
        {
            return;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            test.RunState = RunState.Skipped;
            test.Properties.Add("_SKIPREASON", "This test only runs on Windows");
        }
    }
}
