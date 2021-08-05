using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class TypeNotAllowedException : Exception
    {
        public TypeNotAllowedException(Type type, string path)
        : base($"The type {type.Name} is not allowed on the Halibut message protocol.  Found at {path}")
        {
            DisallowedType = type;
            Path = path;
        }
        
        public Type DisallowedType { get; }
        
        public string Path { get; }
    }
    
    
    public class RegisteredSerializationBinder : ISerializationBinder
    {
        HashSet<Type> allowedTypes = new HashSet<Type>();
        ISerializationBinder baseBinder = new DefaultSerializationBinder();
        
        public RegisteredSerializationBinder(IEnumerable<Type> registeredServiceTypes)
        {
            foreach (var serviceType in registeredServiceTypes)
            {
                var methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                foreach (var method in methods.Where(x => !(x.IsSpecialName || x.DeclaringType == typeof(object))))
                {
                    AllowType(method.ReturnType, $"{serviceType.Name}.{method.Name}:{method.ReturnType.Name}");
                    
                    foreach (var param in method.GetParameters())
                    {
                        AllowType(param.ParameterType,$"{serviceType.Name}.{method.Name}(){param.Name}:{param.ParameterType.Name}");
                    }
                }
            }
        }
        
        void AllowType(Type type, string path)
        {
            if (type == typeof(object) || type == typeof(Task) || type == typeof(Task<>))
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
                AllowType(p.PropertyType, $"{path}.{p.Name}");
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