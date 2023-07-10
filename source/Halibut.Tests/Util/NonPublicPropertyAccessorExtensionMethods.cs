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
        
        
    }
}