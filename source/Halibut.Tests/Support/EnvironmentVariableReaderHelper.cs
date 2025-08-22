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

        public static int? TryReadIntFromEnvironmentVariable(string envVar)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, out var result))
            {
                return result;
            }

            return null;
        }
    }
}