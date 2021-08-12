using System;
using System.Collections.Generic;
using System.Reflection;
using Halibut.Util;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class RegisteredSerializationBinder : ISerializationBinder
    {
        HashSet<Type> allowedTypes = new HashSet<Type>();
        ISerializationBinder baseBinder = new DefaultSerializationBinder();
        
        public RegisteredSerializationBinder(IEnumerable<Type> registeredServiceTypes)
        {
            foreach (var serviceType in registeredServiceTypes)
            {
                foreach (var method in serviceType.GetHalibutServiceMethods())
                {
                    RegisterType(method.ReturnType, $"{serviceType.Name}.{method.Name}:{method.ReturnType.Name}");
                    
                    foreach (var param in method.GetParameters())
                    {
                        RegisterType(param.ParameterType,$"{serviceType.Name}.{method.Name}(){param.Name}:{param.ParameterType.Name}");
                    }
                }
            }
        }
        
        void RegisterType(Type type, string path)
        {
            if (!type.AllowedOnHalibutInterface())
            {
                throw new TypeNotAllowedException(type, path);
            }

            if (!allowedTypes.Add(type))
            {
                // Seen this before, no need to go further
                return;
            }

            if (ShouldRegisterProperties(type))
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    RegisterType(p.PropertyType, $"{path}.{p.Name}");
                }
            }

            foreach (var sub in SubTypesFor(type))
            {
                RegisterType(sub, $"{path}<{sub.Name}>");
            }
        }

        bool ShouldRegisterProperties(Type type)
        {
            return !type.IsEnum && !type.IsValueType && !type.IsPrimitive && !type.IsPointer && !type.HasElementType && type.Namespace != null && !type.Namespace.StartsWith("System");
        }

        IEnumerable<Type> SubTypesFor(Type type)
        {
            if (type.HasElementType)
            {
                yield return type.GetElementType();
            }
            
            if (type.IsGenericType)
            {
                foreach (var t in type.GenericTypeArguments)
                {
                    yield return t;
                }
            }
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            var type = baseBinder.BindToType(assemblyName, typeName);
            return allowedTypes.Contains(type) ? type : null;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            baseBinder.BindToName(serializedType, out assemblyName, out typeName);
        }
    }
}