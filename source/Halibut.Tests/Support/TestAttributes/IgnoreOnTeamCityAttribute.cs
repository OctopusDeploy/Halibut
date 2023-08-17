using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method|AttributeTargets.Class|AttributeTargets.Assembly, AllowMultiple=false, Inherited=false)]
    public class IgnoreOnTeamCityAttribute : NUnitAttribute, IApplyToTest
    {
        readonly string reason;
        
        public IgnoreOnTeamCityAttribute(string reason)
        {
            this.reason = reason;
        }

        public void ApplyToTest(Test test)
        {
            if (test.RunState != RunState.NotRunnable)
            {
                if (TeamCityDetection.IsRunningInTeamCity())
                {
                    test.RunState = RunState.Ignored;
                    test.Properties.Set(PropertyNames.SkipReason, reason);
                }
            }
        }
    }
}
