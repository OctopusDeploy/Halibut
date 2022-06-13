using System;

namespace Halibut.Transport.Protocol
{
    public interface ITypeRegistry
    {
        void Register(params Type[] registeredServiceTypes);
        void RegisterType(Type type, string path, bool ignoreObject);
        bool IsInAllowedTypes(Type type);
        void AddToMessageContract(params Type[] types);
    }
}