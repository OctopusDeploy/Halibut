using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Halibut.Tests
{
    public static class Extensions
    {
        public static string CommaSeperate(this IEnumerable<object> items) => string.Join(", ", items);

        public static T[] InArray<T>(this T item) => new[] { item };

        public static IEnumerable<string> Concat(this IEnumerable<string> items, string str) => items.Concat(new[] { str });

        public static Visibility GetVisibility(this TypeInfo type)
        {
            if (type.IsPublic)
                return Visibility.Public;
            if (type.IsNested)
            {
                if (type.IsNestedPublic)
                    return Visibility.Public;
                if (type.IsNestedPrivate)
                    return Visibility.Private;
                if (type.IsNestedFamily)
                    return Visibility.Protected;
                if (type.IsNestedAssembly)
                    return Visibility.Internal;
                if (type.IsNestedFamORAssem)
                    return Visibility.ProtectedInternal;
            }
            return Visibility.Internal;
        }

        public static Visibility GetVisibility(this MethodBase method)
        {
            if (method.IsPublic)
                return Visibility.Public;
            if (method.IsPrivate)
                return Visibility.Private;
            if (method.IsFamily)
                return Visibility.Protected;
            if (method.IsAssembly)
                return Visibility.Internal;
            if (method.IsFamilyOrAssembly)
                return Visibility.ProtectedInternal;
            return Visibility.Private;
        }

        public static Visibility GetVisibility(this FieldInfo field)
        {
            if (field.IsPublic)
                return Visibility.Public;
            if (field.IsPrivate)
                return Visibility.Private;
            if (field.IsFamily)
                return Visibility.Protected;
            if (field.IsAssembly)
                return Visibility.Internal;
            if (field.IsFamilyOrAssembly)
                return Visibility.ProtectedInternal;
            return Visibility.Private;
        }

        public static bool IsVisible(this MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    [Flags]
    public enum Visibility
    {
        Public = 4,
        Protected = 1,
        Internal = 2,
        ProtectedInternal = 3,
        Private = 0
    }
}