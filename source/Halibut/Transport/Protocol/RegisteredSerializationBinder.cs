using System;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class RegisteredSerializationBinder : DefaultSerializationBinder
    {
        readonly Type[] protocolTypes = { typeof(ResponseMessage), typeof(RequestMessage) };
        readonly ITypeRegistry typeRegistry;

        public RegisteredSerializationBinder() : this(new TypeRegistry()) { } // kept for backwards compatibility.

        internal RegisteredSerializationBinder(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            foreach (var protocolType in protocolTypes) typeRegistry.RegisterType(protocolType, protocolType.Name, true);
        }

        public void Register(params Type[] registeredServiceTypes) // kept for backwards compatibility
        {
            typeRegistry.Register(registeredServiceTypes);
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            var type = base.BindToType(assemblyName, typeName);
            return typeRegistry.IsInAllowedTypes(type) ? type : null;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName) // kept for backwards compatibility
        {
            base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }
}