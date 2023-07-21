using System;
using NUnit.Framework;
using NUnit.Framework.Api;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    /// Halibut tests take time but don't use lots of CPU.
    ///
    /// For local development by default increase the level of parallelism to 2x the number of Cores.
    ///
    /// This can be overriden with the environment variable "CustomLevelOfParallelism" e.g.
    /// CustomLevelOfParallelism=256
    ///
    /// When run in the build the default level is always used.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple=false, Inherited=false )]
    public class CustomLevelOfParallelismAttribute : PropertyAttribute
    {
        public CustomLevelOfParallelismAttribute() : base(LevelOfParallelismAttributePropertyName(), LevelOfParallelism())
        {
        }
        
        public static int LevelOfParallelism()
        {
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                //return LevelOfParallelismInTeamCity();
                return NUnitTestAssemblyRunner.DefaultLevelOfParallelism * 2;
            }
            
            return LevelOfParallelismFromEnvVar()??NUnitTestAssemblyRunner.DefaultLevelOfParallelism * 2;
        }

        static int LevelOfParallelismInTeamCity()
        {
            if (NUnitTestAssemblyRunner.DefaultLevelOfParallelism <= 8)
            {
                // Its unlikely a host with 8 cores is running multiple tests so assume we have access to them all.
                return NUnitTestAssemblyRunner.DefaultLevelOfParallelism;
            }
                
            // Larger hosts are likely to be kubernetes hosts. In this case the host is shared between multiple tests runs
            // which means we can quickly overload the host OS resulting in test flakyness or failures. This is made worse
            // since we are told the CPU limit of the pod which is current set to the number of CPUs on host, meaning
            // on a 32 core host machine we will run 32 tests in parallel!
            // Lets reduce the level of parallel tests in that case.
            // This is probably not needed on linux, where we don't see test failures.
            return NUnitTestAssemblyRunner.DefaultLevelOfParallelism / 2;
        }

        static int? LevelOfParallelismFromEnvVar()
        {
            var nunitLevelOfParallelismSetting = Environment.GetEnvironmentVariable("CustomLevelOfParallelism");
            if (!string.IsNullOrEmpty(nunitLevelOfParallelismSetting))
            {
                if (int.TryParse(nunitLevelOfParallelismSetting, out var level))
                {
                    return level;
                }
            }

            return null;
        }

        static string LevelOfParallelismAttributePropertyName()
        {
            string propertyName = typeof(LevelOfParallelismAttribute).Name;
            if ( propertyName.EndsWith( "Attribute" ) )
                propertyName = propertyName.Substring( 0, propertyName.Length - 9 );
            return propertyName;
        }
    }
}