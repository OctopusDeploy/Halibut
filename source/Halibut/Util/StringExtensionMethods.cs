using System;
using System.Diagnostics.CodeAnalysis;

namespace Halibut.Util
{
    public static class StringExtensionMethods
    {
        [return: NotNullIfNotNull("str")]
        public static Guid? ToGuid(this string? str)
        {
            if (str == null)
            {
                return null;
            }

            return Guid.Parse(str);
        }
    }
}