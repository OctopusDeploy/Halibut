using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Halibut.Portability
{
    public static class TypeExtensions
    {
        public static Assembly Assembly(this Type type)
        {
            return type.Assembly;
        }

        public static bool IsValueType(this Type type)
        {
            return type.IsValueType;
        }

        public static bool IsNullable(this Type type)
        {
            return !type.IsValueType() || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
