using System;
using System.Runtime.InteropServices;
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
                return LevelOfParallelismInTeamCity();
            }
            
            return LevelOfParallelismFromEnvVar() ?? NUnitTestAssemblyRunner.DefaultLevelOfParallelism * 2;
        }

        /// <summary>
        /// 1 CPU = 2 Parallel Tests
        /// 2 CPU = 2 Parallel Tests
        /// 3 CPU = 2 Parallel Tests
        /// 4 CPU = 2 Parallel Tests
        /// 5 CPU = 3 Parallel Tests
        /// 6 CPU = 4 Parallel Tests
        /// 7 CPU = 5 Parallel Tests
        /// 8 CPU = 6 Parallel Tests
        /// 9 CPU = 7 Parallel Tests
        /// 10 CPU = 8 Parallel Tests
        /// 11 CPU = 8 Parallel Tests
        /// 12 CPU = 8 Parallel Tests
        /// 13 CPU = 8 Parallel Tests
        /// 14 CPU = 8 Parallel Tests
        /// </summary>
        static int LevelOfParallelismInTeamCity()
        {
            var max = 8;
            var min = 2;
            var defaultBasedOnCpu = NUnitTestAssemblyRunner.DefaultLevelOfParallelism;
            
            return Math.Min(Math.Max(min, defaultBasedOnCpu - 2), max);
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