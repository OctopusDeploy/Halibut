#nullable enable
using System;

namespace Halibut.Util
{
    public static class HalibutClientExceptionExtensionMethods
    {
        public static bool InnerExceptionTypeIs<T>(this HalibutClientException exception)
        {
            return exception.InnerExceptionType == typeof(T);
        }
    }
}