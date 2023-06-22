using System;

namespace Halibut.Tests.Util
{
    public class TeamCityDetection
    {
        public static bool IsRunningInTeamCity() 
        { 
            string environmentVariableValue = Environment.GetEnvironmentVariable("TEAMCITY_VERSION"); 
            if (!string.IsNullOrEmpty(environmentVariableValue)) 
            { 
                return true; 
            } 
            return false; 
        } 
    }
}