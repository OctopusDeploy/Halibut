using System;

namespace Halibut.Tests.Support
{
    public class TeamCityDetection
    {
        public static bool IsRunningInTeamCity()
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
        }
    }
}