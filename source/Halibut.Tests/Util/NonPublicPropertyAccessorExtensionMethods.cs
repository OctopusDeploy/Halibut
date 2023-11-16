using System;
using System.Reflection;

namespace Halibut.Tests.Util
{
    public static class NonPublicPropertyAccessorExtensionMethods
    {
        public static void ReflectionSetFieldValue(this Type type, string fieldName, object value)
        {
            const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.FlattenHierarchy | BindingFlags.Static;

            var field = type.GetField(fieldName, bindingFlags);
            if (field is null) throw new InvalidOperationException($"Failed to find field {fieldName} on type {type.FullName}");
            field.SetValue(null, value);
        }

        public static T ReflectionGetFieldValue<T>(this object getFrom, string fieldName)
        {
            var value = ReflectionGetFieldValue(getFrom, fieldName);
            return (T) value;
        }

        public static object ReflectionGetFieldValue(this object getFrom, string fieldName)
        {
            const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.FlattenHierarchy | BindingFlags.Static;

            var type = getFrom.GetType();

            var field = type.GetField(fieldName, bindingFlags);
            if (field is null) throw new InvalidOperationException($"Failed to find field {fieldName} on type {type.FullName}");

            var value = field.GetValue(getFrom);
            return value;
        }
    }
}