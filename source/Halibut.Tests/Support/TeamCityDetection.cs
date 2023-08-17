using System;

namespace Halibut.Tests.Support
{
    public class TeamCityDetection
    {
        public static bool IsRunningInTeamCity()
        {
            return _IsRunningInTeamcity;
        }

        static readonly bool _IsRunningInTeamcity = ComputeIsRunningInTeamCity();

        static bool ComputeIsRunningInTeamCity()
        {
            foreach (var tcEnvVar in new[] {"TEAMCITY_VERSION", "TEAMCITY_BUILD_ID"})
            {
                var environmentVariableValue = Environment.GetEnvironmentVariable(tcEnvVar);
                if (!string.IsNullOrEmpty(environmentVariableValue)) return true;
            }

            return false;
        }
    }
}