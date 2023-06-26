using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Transport.Caching
{
    static class ParameterCacheKeys
    {
        public static string GenerateCacheKey(object parameter)
        {
            if (parameter == null) return string.Empty;
            if (parameter is string key) return key;
            if (parameter is Guid guid) return guid.ToString();
            if (parameter is DateTime dateTime) return dateTime.ToString("O");
            if (parameter is DateTimeOffset dateTimeOffset) return dateTimeOffset.ToString("O");
            if (parameter is IEnumerable enumerable) return GenerateCacheKey(enumerable.Cast<object>());

            throw new ArgumentOutOfRangeException($"Parameter of type {parameter.GetType()} cannot be used as a cache key.");
        }

        static string GenerateCacheKey(IEnumerable<object> parameter)
        {
            if (parameter == null) return string.Empty;
            return "[" + string.Join(",", parameter) + "]";
        }
    }
}