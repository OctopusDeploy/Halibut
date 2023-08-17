using System;
using NUnit.Framework.Constraints;

namespace Halibut.Tests.Support
{
    public class TeamCityDetection
    {
        public static Lazy<bool> IsRunningInTeamcityLazy = new Lazy<bool>(() =>
        {
            foreach (var tcEnvVar in new string[] { "TEAMCITY_VERSION", "TEAMCITY_BUILD_ID" })
            {
                string environmentVariableValue = Environment.GetEnvironmentVariable(tcEnvVar);
                if (!string.IsNullOrEmpty(environmentVariableValue))
                {
                    return true;
                }
            }
            return false;
        });
        
        public static bool IsRunningInTeamCity()
        {
            return IsRunningInTeamcityLazy.Value;
        }
    }
}