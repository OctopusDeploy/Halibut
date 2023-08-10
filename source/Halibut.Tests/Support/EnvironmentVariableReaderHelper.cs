using System;
using Castle.Core.Internal;

namespace Halibut.Tests.Support
{
    public class EnvironmentVariableReaderHelper
    {
        public static bool EnvironmentVariableAsBool(string envVar, bool defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value!.Equals("true");
        }
    }
}