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

            if (type.IsEnum || type.IsPrimitive)
            {
                return;
            }
            
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                RegisterType(p.PropertyType, $"{path}.{p.Name}");
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