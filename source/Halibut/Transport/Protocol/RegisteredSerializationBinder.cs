using System;
using System.Collections.Generic;
using System.Reflection;
using Halibut.Util;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class RegisteredSerializationBinder : ISerializationBinder
    {
        readonly Type[] protocolTypes = new[] { typeof(ResponseMessage), typeof(RequestMessage) };
        readonly HashSet<Type> allowedTypes = new HashSet<Type>();
        readonly ISerializationBinder baseBinder = new DefaultSerializationBinder();
        
        public RegisteredSerializationBinder()
        {
            foreach (var protocolType in protocolTypes)
            {
                RegisterType(protocolType, protocolType.Name, true);    
            }
        }

        public void Register(params Type[] registeredServiceTypes)
        {
            foreach (var serviceType in registeredServiceTypes)
            {
                foreach (var method in serviceType.GetHalibutServiceMethods())
                {
                    RegisterType(method.ReturnType, $"{serviceType.Name}.{method.Name}:{method.ReturnType.Name}", false);
                    
                    foreach (var param in method.GetParameters())
                    {
                        RegisterType(param.ParameterType,$"{serviceType.Name}.{method.Name}(){param.Name}:{param.ParameterType.Name}", false);
                    }
                }
            }
        }
        
        void RegisterType(Type type, string path, bool ignoreObject)
        {
            if (!type.AllowedOnHalibutInterface())
            {
                if (ignoreObject)
                {
                    return;
                }
                
                throw new TypeNotAllowedException(type, path);
            }

            lock (allowedTypes)
            {
                if (!allowedTypes.Add(type))
                {
                    // Seen this before, no need to go further
                    return;
                }
            }

            if (ShouldRegisterProperties(type))
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    RegisterType(p.PropertyType, $"{path}.{p.Name}", ignoreObject);
                }
            }

            foreach (var sub in SubTypesFor(type))
            {
                RegisterType(sub, $"{path}<{sub.Name}>", ignoreObject);
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
            lock (allowedTypes)
            {
                return allowedTypes.Contains(type) ? type : null;
            }
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            baseBinder.BindToName(serializedType, out assemblyName, out typeName);
        }
    }
}