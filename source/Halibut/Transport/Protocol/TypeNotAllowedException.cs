using System;

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
}