using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Halibut.Transport.Caching
{
    class CachingKeyGenerator
    {
        const char LinkChar = ':';

        internal string GetCacheKey(MethodInfo methodInfo, object[] args, string prefix)
        {
            var methodArguments = args?.Any() == true
                ? args.Select(ParameterCacheKeys.GenerateCacheKey)
                : new[] { "0" };
            return GenerateCacheKey(methodInfo, prefix, methodArguments);
        }

        string GetCacheKeyPrefix(MethodInfo methodInfo, string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix)) return $"{prefix}{LinkChar}";

            var typeName = methodInfo.DeclaringType?.Name;
            var methodName = methodInfo.Name;

            return $"{typeName}{LinkChar}{methodName}{LinkChar}";
        }

        string GenerateCacheKey(MethodInfo methodInfo, string prefix, IEnumerable<string> parameters)
        {
            var cacheKeyPrefix = GetCacheKeyPrefix(methodInfo, prefix);

            var builder = new StringBuilder();
            builder.Append(cacheKeyPrefix);
            builder.Append(string.Join(LinkChar.ToString(), parameters));
            return builder.ToString();
        }
    }
}